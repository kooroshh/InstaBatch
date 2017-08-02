using System;
using System.Collections.Generic;
using System.Text;

namespace InstaBatch
{
    struct UserStruct
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public int Followers { get; set; }
        public int Following { get; set; }
        public bool Locked { get; set; }
        public UserStruct(string username, string password)
        {
            Username = username;
            Password = password;
            Followers = 0;
            Following = 0;
            Locked = false;
        }
    }
}
