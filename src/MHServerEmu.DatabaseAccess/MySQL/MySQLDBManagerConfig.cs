using MHServerEmu.Core.Config;
using System.Runtime.InteropServices;

namespace MHServerEmu.DatabaseAccess.MySqlDB
{
    public class MySQLDBManagerConfig : ConfigContainer
    {
        public string MySqlIP { get; private set; } = "127.0.0.1";

        public string MySqlDBName { get; private set; } = "Mheroes";

        public string MySqlUsername { get; private set; } = "MHeroesSQL";

        public string MySqlPw { get; private set; } = "password123@";
    }
}
