using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using JoyOI.OnlineJudge.Models;

namespace Tyvj.Migration.Lib
{
    public static class TyvjUser
    {
        public async static Task<bool> CheckUserCredentialAsync(string username, string password)
        {
            using (var conn = new MySqlConnection(Startup.Config["Tyvj:ConnectionString"]))
            {
                await conn.OpenAsync();
                using (var cmd = new MySqlCommand($"SELECT * FROM `users` WHERE `username` = @username", conn))
                {
                    cmd.Parameters.Add(new MySqlParameter("username", username));
                    using (var dr = await cmd.ExecuteReaderAsync())
                    {
                        if (!await dr.ReadAsync())
                            return false;
                        var pwd = dr["password"].ToString();
                        if (pwd.Length == Crypto.MD5Length)
                        {
                            return pwd.ToLower() == Crypto.MD5(password);
                        }
                        else if (pwd.Length == Crypto.SHA1Length)
                        {
                            return pwd.ToLower() == Crypto.SHA1(password);
                        }
                        else
                        {
                            return pwd == password;
                        }
                    }
                }
            }
        }

        public async static Task<bool> CheckUserMigratedAsync(string username)
        {
            using (var conn = new MySqlConnection(Startup.Config["Tyvj:ConnectionString"]))
            {
                await conn.OpenAsync();
                using (var cmd = new MySqlCommand($"SELECT * FROM `users` WHERE `username` = @username", conn))
                {
                    cmd.Parameters.Add(new MySqlParameter("username", username));
                    using (var dr = await cmd.ExecuteReaderAsync())
                    {
                        if (!await dr.ReadAsync())
                            throw new Exception("User not found");

                        return !string.IsNullOrEmpty(dr["joyoi_name"].ToString());
                    }
                }
            }
        }

        public async static Task<string> GetPhoneNumberAsync(string username)
        {
            try
            {
                using (var conn = new MySqlConnection(Startup.Config["Tyvj:ConnectionString"]))
                {
                    await conn.OpenAsync();
                    using (var cmd = new MySqlCommand($"SELECT `phone` FROM `users` WHERE `username` = @username", conn))
                    {
                        cmd.Parameters.Add(new MySqlParameter("username", username));
                        return (await cmd.ExecuteScalarAsync()).ToString();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public async static Task<string> GetEmailAsync(string username)
        {
            try
            {
                using (var conn = new MySqlConnection(Startup.Config["Tyvj:ConnectionString"]))
                {
                    await conn.OpenAsync();
                    using (var cmd = new MySqlCommand($"SELECT `email` FROM `users` WHERE `username` = @username", conn))
                    {
                        cmd.Parameters.Add(new MySqlParameter("username", username));
                        return (await cmd.ExecuteScalarAsync()).ToString();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public async static Task<int> GetUserIdAsync(string username)
        {
            try
            {
                using (var conn = new MySqlConnection(Startup.Config["Tyvj:ConnectionString"]))
                {
                    await conn.OpenAsync();
                    using (var cmd = new MySqlCommand($"SELECT `id` FROM `users` WHERE `username` = @username", conn))
                    {
                        cmd.Parameters.Add(new MySqlParameter("username", username));
                        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }
                }
            }
            catch
            {
                return -1;
            }
        }

        public async static Task<(IEnumerable<int>, IEnumerable<int>)> GetCachedAcAndTriedProblemsAsync(string username)
        {
            try
            {
                using (var conn = new MySqlConnection(Startup.Config["Tyvj:ConnectionString"]))
                {
                    await conn.OpenAsync();
                    using (var cmd = new MySqlCommand($"SELECT `id` FROM `users` WHERE `username` = @username", conn))
                    {
                        cmd.Parameters.Add(new MySqlParameter("username", username));
                        using (var dr = await cmd.ExecuteReaderAsync())
                        {
                            if (!await dr.ReadAsync())
                                throw new Exception();

                            var accepted = dr["accepted_list"].ToString();
                            var tried = dr["submit_list"].ToString();

                            return (tried.Split('|').Select(x => Convert.ToInt32(x)), accepted.Split('|').Select(x => Convert.ToInt32(x)));
                        }
                    }
                }
            }
            catch
            {
                return (null, null);
            }
        }

        public async static Task<IEnumerable<int>> GetOwnedProblemsAsync(int userid)
        {
            var ret = new List<int>(10);
            using (var conn = new MySqlConnection(Startup.Config["Tyvj:ConnectionString"]))
            {
                await conn.OpenAsync();
                using (var cmd = new MySqlCommand($"SELECT `id` FROM `problems` WHERE `user_id` = { userid }", conn))
                using (var dr = await cmd.ExecuteReaderAsync())
                {
                    while (await dr.ReadAsync())
                    {
                        ret.Add(Convert.ToInt32(dr["id"].ToString()));
                    }
                }
            }
            return ret;
        }

        public async static Task<int> GetCoinsAsync(string username)
        {
            try
            {
                using (var conn = new MySqlConnection(Startup.Config["Tyvj:ConnectionString"]))
                {
                    await conn.OpenAsync();
                    using (var cmd = new MySqlCommand($"SELECT `id` FROM `users` WHERE `username` = @username", conn))
                    {
                        cmd.Parameters.Add(new MySqlParameter("username", username));
                        using (var dr = await cmd.ExecuteReaderAsync())
                        {
                            if (!await dr.ReadAsync())
                                throw new Exception();

                            return Convert.ToInt32(dr["coins"].ToString());
                        }
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        public static async Task<IEnumerable<JudgeStatus>> GetStatusesAsync(int userId)
        {
            var ret = new List<JudgeStatus>(10);

            using (var conn = new MySqlConnection(Startup.Config["Tyvj:ConnectionString"]))
            {
                await conn.OpenAsync();
                using (var cmd = new MySqlCommand($"SELECT * FROM `statuses` WHERE `user_id` = { userId } AND `problem_id` > 1000", conn))
                using (var dr = await cmd.ExecuteReaderAsync())
                {
                    while (await dr.ReadAsync())
                    {
                        try {
                            ret.Add(new JudgeStatus
                            {
                                Code = dr["code"].ToString(),
                                ProblemId = "tyvj-" + Convert.ToInt32(dr["problem_id"].ToString()),
                                Language = LanguageIdToString(Convert.ToInt32(dr["language"].ToString())),
                                Result = ResultIdToEnum(Convert.ToInt32(dr["result"].ToString())),
                                MemoryUsedInByte = Convert.ToInt32(dr["memory_usage"].ToString()) * 1024,
                                TimeUsedInMs = Convert.ToInt32(dr["time_usage"].ToString()),
                                CreatedTime = Convert.ToDateTime(dr["time"].ToString()),
                                SubStatuses = (await GetSubStatuses(Convert.ToInt32(dr["id"].ToString()))).ToList()
                            });
                        } catch { }
                    }
                }
            }

            return ret;
        }

        public static async Task<IEnumerable<SubJudgeStatus>> GetSubStatuses(int id)
        {
            var ret = new List<SubJudgeStatus>(10);
            using (var conn = new MySqlConnection(Startup.Config["Tyvj:ConnectionString"]))
            {
                await conn.OpenAsync();
                var i = 1;
                using (var cmd = new MySqlCommand($"SELECT * FROM `judge_tasks` WHERE `status_id` = { id }", conn))
                using (var dr = await cmd.ExecuteReaderAsync())
                {
                    while (await dr.ReadAsync())
                    {
                        try
                        {
                            ret.Add(new SubJudgeStatus
                            {
                                SubId = i++,
                                Hint = dr["hint"].ToString(),
                                TimeUsedInMs = Convert.ToInt32(dr["time_usage"].ToString()),
                                MemoryUsedInByte = Convert.ToInt32(dr["memory_usage"]) * 1024,
                                Result = ResultIdToEnum(Convert.ToInt32(dr["result"]))
                            });
                        }
                        catch { }
                    }
                }
            }
            return ret;
        }

        public static async Task LockTyvjUserAsync(string joyoiName, string tyvjName)
        {
            using (var conn = new MySqlConnection(Startup.Config["Tyvj:ConnectionString"]))
            {
                await conn.OpenAsync();
                using (var cmd = new MySqlCommand($"UPDATE `users` SET `joyoi_name` = @joyoi_name WHERE `username` = @username", conn))
                {
                    cmd.Parameters.Add(new MySqlParameter("joyoi_name", joyoiName));
                    cmd.Parameters.Add(new MySqlParameter("username", tyvjName));
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static string LanguageIdToString(int id)
        {
            var dic = new[] { "C", "C++", "C++", "Java", "Pascal", "Python", "Python", "Ruby", "C#", "VB.NET", "F#", "C++" };
            return dic[id];
        }

        public static JudgeResult ResultIdToEnum(int id)
        {
            var dic = new[] { JudgeResult.Accepted, JudgeResult.PresentationError, JudgeResult.WrongAnswer, JudgeResult.OutputExceeded, JudgeResult.TimeExceeded, JudgeResult.MemoryExceeded, JudgeResult.RuntimeError, JudgeResult.CompileError, JudgeResult.SystemError, JudgeResult.Hacked, JudgeResult.Running, JudgeResult.Pending, JudgeResult.Hidden };
            return dic[id];
        }
    }
}
