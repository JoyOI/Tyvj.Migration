using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JoyOI.UserCenter.SDK;
using JoyOI.OnlineJudge.Models;
using Newtonsoft.Json;

namespace Tyvj.Migration.Controllers
{
    public class HomeController : Controller
    {
        private static Random random = new Random();

        private class RegisterBody
        {
            public int code { get; set; }
            public DateTime send { get; set; }
            public DateTime expire { get; set; }
            public string phone { get; set; }
        }

        // GET: /<controller>/
        public IActionResult Index()
        {
            return View();
        }
        
        [HttpPost]
        public async Task<IActionResult> Step2(string phone, [FromServices] JoyOIUC UC, [FromServices] AesCrypto Aes)
        {
            if (await UC.IsPhoneExistAsync(phone))
            {
                return Content("您的手机号码已在Joy OI中注册，请更换后重试！");
            }

            if (Request.Cookies["register"] != null)
            {
                var register = JsonConvert.DeserializeObject<RegisterBody>(Aes.Decrypt(Request.Cookies["register"]));
                if (register.send.AddMinutes(2) > DateTime.Now)
                {
                    return Content("您刚刚已经请求发送过验证码，请稍等2分钟后再试！");
                }
            }

            var code = random.Next(100000, 999999);

            Response.Cookies.Append("register", Aes.Encrypt(JsonConvert.SerializeObject(new RegisterBody
            {
                code = code,
                phone = phone,
                expire = DateTime.Now.AddMinutes(30),
                send = DateTime.Now
            })));

            await UC.SendSmsAsync(phone, "您正在进行Tyvj账号资料转移至Joy OI的操作，验证码为：" + code);

            return View();
        }

        [HttpPost]
        public IActionResult Step3(int code, [FromServices] AesCrypto Aes)
        {
            if (Request.Cookies["register"] != null)
            {
                var register = JsonConvert.DeserializeObject<RegisterBody>(Aes.Decrypt(Request.Cookies["register"]));
                if (code == register.code)
                {
                    Response.Cookies.Append("phone", Aes.Encrypt(register.phone));
                    return View();
                }
                else
                {
                    return Content("您输入的验证码不正确！");
                }
            }
            else
            {
                return Content("非法请求");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Step4(string username, string password, [FromServices] JoyOIUC UC, [FromServices] AesCrypto Aes)
        {
            if (Request.Cookies["phone"] != null)
            {
                if (await Lib.TyvjUser.CheckUserCredentialAsync(username, password))
                {
                    if (await Lib.TyvjUser.CheckUserMigratedAsync(username))
                    {
                        return Content("您的账号已经绑定Joy OI，请勿重复绑定！");
                    }

                    ViewBag.Invalid = false;
                    ViewBag.EmailInvalid = false;
                    Response.Cookies.Append("tyvj", Aes.Encrypt(username));
                    Response.Cookies.Append("tyvjp", Aes.Encrypt(password));
                    var regex = new Regex("^[\u3040-\u309F\u30A0-\u30FF\u4e00-\u9fa5A-Za-z0-9_-]{4,32}$");
                    if (regex.IsMatch(username))
                    {
                        if (await UC.IsUsernameExistAsync(username))
                        {
                            ViewBag.Invalid = true;
                        }
                        else
                        {
                            ViewBag.Username = username;
                        }
                    }
                    else
                    {
                        ViewBag.Invalid = true;
                    }

                    var email = await Lib.TyvjUser.GetEmailAsync(username);
                    if (await UC.IsEmailExistAsync(email))
                    {
                        ViewBag.EmailInvalid = true;
                    }
                    else
                    {
                        ViewBag.Email = email;
                    }

                    return View();
                }
                else
                {
                    return Content("您的Tyvj用户名或密码不正确！");
                }
            }
            else
            {
                return Content("非法请求");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Step5(string username, string email, [FromServices] JoyOIUC UC, [FromServices] AesCrypto Aes)
        {
            if (Request.Cookies["phone"] != null && Request.Cookies["tyvj"] != null && Request.Cookies["tyvjp"] != null)
            {
                if (await UC.IsPhoneExistAsync(Aes.Decrypt(Request.Cookies["phone"])))
                {
                    return Content("手机号码已被注册，请更换后重试！");
                }

                var regex = new Regex("^[\u3040-\u309F\u30A0-\u30FF\u4e00-\u9fa5A-Za-z0-9_-]{4,32}$");
                if (regex.IsMatch(username))
                {
                    if (await UC.IsUsernameExistAsync(username))
                    {
                        return Content("用户名已经被占用，请更换后重试！");
                    }
                    else
                    {
                        if (await UC.IsEmailExistAsync(email))
                        {
                            return Content("您的Email已经被注册，请更换后再试！");
                        }

                        Guid openId;
                        try
                        {
                            openId = await UC.InsertUserAsync(username, Aes.Decrypt(Request.Cookies["tyvjp"]), Aes.Decrypt(Request.Cookies["phone"]), email);
                        }
                        catch
                        {
                            return Content("您的Joy OI通行证创建失败，可能因为您的手机号码、电子邮箱、或用户名已经在Joy OI上存在，请您更换以上信息后重新尝试迁移！");
                        }
                        using (var client = new HttpClient() { BaseAddress = new Uri("http://api.oj.joyoi.cn") })
                        using (var response = await client.PutAsync("/api/user/session", new StringContent(JsonConvert.SerializeObject(new { username = username, password = Aes.Decrypt(Request.Cookies["tyvjp"]) }))))
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                throw new Exception("激活Joy OI账号失败\n" + await response.Content.ReadAsStringAsync());
                            }
                        }

                        // 处理评测记录
                        var tyvjId = await Lib.TyvjUser.GetUserIdAsync(Aes.Decrypt(Request.Cookies["tyvj"]));
                        var statuses = await Lib.TyvjUser.GetStatusesAsync(tyvjId);
                        var builder = new DbContextOptionsBuilder<OnlineJudgeContext>();
                        builder.UseMySql(Startup.Config["Data:MySql"]);
                        using (var db = new OnlineJudgeContext(builder.Options))
                        {
                            foreach (var x in statuses)
                            {
                                try
                                {
                                    x.UserId = openId;
                                    db.JudgeStatuses.Add(x);
                                    await db.SaveChangesAsync();
                                } catch { }
                            }
                        }

                        // 处理通过题目缓存
                        var cache = await Lib.TyvjUser.GetCachedAcAndTriedProblemsAsync(Aes.Decrypt(Request.Cookies["tyvj"]));
                        using (var db = new OnlineJudgeContext(builder.Options))
                        {
                            var user = await db.Users.Where(x => x.UserName == Aes.Decrypt(Request.Cookies["tyvj"])).SingleAsync();
                            if (cache.Item1 != null)
                                user.TriedProblems = JsonConvert.SerializeObject(cache.Item1.Select(x => "tyvj-" + x).ToList());
                            if (cache.Item2 != null)
                                user.PassedProblems = JsonConvert.SerializeObject(cache.Item2.Select(x => "tyvj-" + x).ToList());
                            await db.SaveChangesAsync();
                        }

                        // 处理题目所有权
                        var problems = await Lib.TyvjUser.GetOwnedProblemsAsync(tyvjId);
                        using (var db = new OnlineJudgeContext(builder.Options))
                        {
                            foreach (var x in problems)
                            {
                                try
                                {
                                    db.UserClaims.Add(new Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>() {
                                        ClaimType = "Edit Problem",
                                        ClaimValue = "tyvj-" + x,
                                        UserId = openId
                                    });
                                    await db.SaveChangesAsync();
                                }
                                catch { }
                            }
                        }

                        await Lib.TyvjUser.LockTyvjUserAsync(username, Aes.Decrypt(Request.Cookies["tyvj"]));

                        return View();
                    }
                }
                else
                {
                    return Content("新用户名不合法，用户名至少应为4为，支持中英文，不允许包含特殊字符");
                }
            }
            else
            {
                return Content("非法请求");
            }
        }
    }
}
