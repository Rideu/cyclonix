

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

using Cyclonix.Utils;

namespace Cyclonix.Net
{
    public partial class NetServer
    {
        public bool ChillEnabled { get; set; } = true;

        public void AddSubnetBan(params string[] subnets)
        {
            subnetBans.AddRange(subnets);
        }

        List<string> subnetBans = new List<string>();

        [DataContract]
        struct Chill
        {
            [DataMember]
            public int rem;
            [DataMember]
            public DateTime ts;
            [DataMember]
            public string reason;
            [DataMember]
            public string meta;
        }

        List<Chill> ChilloutList = new List<Chill>();

        /// <summary> Ban remote from accessing the server until timeout is expired</summary>
        public void ChillRemote(NetNode n, TimeSpan timeout, string reason = null, string addata = null)
        {
            lock (chillWriter)
            {

                if (!ChillEnabled) return;

                if (ChilloutList.FirstOrDefault(c => c.rem == n.ShortIP).rem != 0)
                    return;

                var blockend = DateTime.Now.Add(timeout);
                var chill = new Chill { rem = n.ShortIP, ts = blockend, reason = reason, meta = addata };
                ChilloutList.Add(chill);
                var chillstr = JSON.Stringify(chill);
                chillWriter.WriteLine(chillstr);
            }
        }

        FileStream chillFile;

        StreamWriter chillWriter;

        void LoadChillRemotes()
        {
            LogWriter.LogMsgTag("[L]", "Loading chills...", ConsoleColor.Yellow);

            var chillDir = StoragePath + "\\internal";
            Directory.CreateDirectory(chillDir); 
            chillFile = File.Open(chillDir + "\\chill.json", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

            StreamReader sr = new StreamReader(chillFile);

            while (!sr.EndOfStream)
            {
                var chillstr = sr.ReadLine();

                Chill chilly = default;
                try
                {
                    chilly = JSON.Parse<Chill>(chillstr);
                }
                catch (Exception e)
                {

                }

                if (chilly.rem == 0)
                    continue;

                if (ChilloutList.FirstOrDefault(c => c.rem == chilly.rem).rem != 0)
                    continue;

                ChilloutList.Add(chilly);
            } 

            chillWriter = new StreamWriter(chillFile);
            LogWriter.LogMsgTag("[L]", $"Chills loaded ({ChilloutList.Count} entries)", ConsoleColor.Yellow);
             
        }
    }
}
