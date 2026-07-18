using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Be.IO;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace xayrga.bast
{
    class JASEPacker
    {
        private Stream stream;
        private BeBinaryWriter writer;
        //private string projectDirectory;



        private static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public void doPack(string projectFolder, string outFile, string packOrder = "guess")
        {
            cmdarg.assert(!Directory.Exists(projectFolder), $"Folder {projectFolder} does not exist");
            cmdarg.assert(!File.Exists($"{projectFolder}/root.json"), $"File {projectFolder}/root.json does not exist");

            stream = File.Open(outFile, FileMode.OpenOrCreate, FileAccess.Write);
            writer = new BeBinaryWriter(stream);
            var JData = File.ReadAllText($"{projectFolder}/root.json");
            var rootIndex = JsonConvert.DeserializeObject<JToken>(JData);
            var version = (string)rootIndex["format"];
            switch (version)
            {
                case "JASE":
                    {
                        var project = JsonConvert.DeserializeObject<JASEProject>(JData);
                        packJASE(projectFolder, project, packOrder);
                        break;
                    }
            }
        }


        private void packJASE(string projectFolder, JASEProject project, string packOrder = "guess")
        {


            writer.Write((short)project.version);
            writer.Write((short)project.revision);


            JASECategory[] categories = new JASECategory[0x12];
            var totalSoundCount = 0;
            for (int i = 0; i < 0x12; i++)
                if (project.includes[i] != null)
                {
                    var jDat = File.ReadAllText($"{projectFolder}/{project.includes[i]}");
                    categories[i] = JsonConvert.DeserializeObject<JASECategory>(jDat);
                    categories[i].count = (ushort)categories[i].sounds.Length;
                    categories[i].index = (byte)i;
                    
                    totalSoundCount += categories[i].sounds.Length;
                }
                else                
                    categories[i] = new JASECategory { startID = 0, count = 0, index = 0xFF };                

            writer.Write((ushort)totalSoundCount);

            var catSorted = new JASECategory[0x12]; // strong ref to JASECategory. Changes here will affect final objects
            Array.Copy(categories, catSorted, 0x12);
            // sort 

            if (packOrder == "guess")
            {
                for (int i = 0; i < 0x12; i++)
                {
                    for (int j = 0; j < 0x12; j++)
                    {
                        var current = catSorted[i];
                        var cmp = catSorted[j];
                        if (cmp.startID > current.startID)
                        {
                            catSorted[j] = current;
                            catSorted[i] = cmp;
                        }
                    }
                }
                var guessed = "";
                for (int i=0; i < catSorted.Length;i++)
                {
                    guessed += $"{catSorted[i].index:X2}";
                }
                Console.WriteLine($"No order table... Guessing: {guessed}");
            } else
            {
                var orderTable = StringToByteArray(packOrder);
                if (orderTable.Length < 0x12)
                    cmdarg.assert("Packorder is specified! Must be 0x12 bytes!");
                for (int i = 0; i < 0x12; i++)
                    if (orderTable[i] != 0xFF)
                        catSorted[i] = categories[orderTable[i]];
                    else
                        catSorted[i] = new JASECategory { startID = 0, count = 0, index = 0xFF };
            }

            totalSoundCount = 0; 
            for (int i = 0; i < 0x12; i++)
            {
                var cat = catSorted[i];
                if (cat.count > 0)
                {
                    cat.startID = (ushort)totalSoundCount;
                    totalSoundCount += cat.count;  
                }
            }


            for (int i = 0; i < 0x12; i++)
            {
                var cat = categories[i];
                if (cat.count > 0)
                {
                    writer.Write(cat.count);
                    writer.Write(cat.startID);
                   
                } else
                {
                    writer.Write(0);
                }
            }
            writer.Write((short)0u);
          
            for (int i=0; i < catSorted.Length; i++)
            {
                var cat = catSorted[i];
                if (cat.count > 0)
                {
                    for (int b=0; b < cat.count; b++)
                    {
                        var snd = cat.sounds[b];
                        writer.Write(snd.sflags);
                        writer.Write(snd.pflags);
                        writer.Write(snd.uflags1);
                        writer.Write(snd.uflags2);
                        writer.Write(snd.type);
                        writer.Write(snd.loadMode);
                        writer.Write(snd.unk3);
                        writer.Write(snd.pitch);
                        writer.Write(snd.volume);
                        writer.Write((short)0);
                    }
                }
            }

            writer.Flush();
            writer.Close();
        }
     }
  }

