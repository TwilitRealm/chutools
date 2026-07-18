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
    class JASEUnpacker
    {
        private Stream stream;
        private BeBinaryReader reader;

        public JAudioTableType getType(BeBinaryReader reader)
        {
            return JAudioTableType.AUDIOSOUNDEFFECT;
        }

        public void doUnpack(string file, string projectDir)
        {
            cmdarg.assert(!File.Exists(file), $"File {file} does not exist");
            stream = File.Open(file, FileMode.Open);
            reader = new BeBinaryReader(stream);
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory($"{projectDir}/includes");
            var unpackMode = getType(reader);
            switch (unpackMode)
            {
                case JAudioTableType.AUDIOSOUNDEFFECT:
                    unpackJASE(projectDir);
                    break;
            }
        }

        private void unpackJASE(string projectDir)
        {
            var wl = new JASE();
            wl.readInfo(reader, false);
            wl.loadCategories(reader, false);

            var catSorted = wl.Categories;
        
            var totalID = 0;
             // load indices 
            for (int i = 0; i < catSorted.Length; i++)
            {
                var cat = catSorted[i];

                    reader.BaseStream.Position = 0x50 + cat.startID * 0x10; // just like daddy nintendo does. 
                    if (cat.count > 0)
                    {
                        for (int q = 0; q < cat.count; q++)
                        {
                            cat.sounds[q] = new JASESound();
                            cat.sounds[q].readInfo(reader, false);
                            cat.sounds[q].id = totalID;
                            cat.sounds[q].index = q;
                            totalID++;
                        }
                    }
             }
            


            var w = new JASEProject();
            w.version = wl.u1;
            w.revision = wl.u2;
            w.includes = new string[0x12]; 

            for (int i=0; i < 0x12; i++)
            {
                var cat = catSorted[i];
                if (cat!=null && cat.count > 0 )
                {
                    var catSer = JsonConvert.SerializeObject(cat,Formatting.Indented);
                    File.WriteAllText($"{projectDir}/includes/0x{i + 1:X}.json", catSer);
                    w.includes[i] = $"includes/0x{i + 1:X}.json";
                }
            }
            var outfile = JsonConvert.SerializeObject(w, Formatting.Indented);
            File.WriteAllText($"{projectDir}/root.json",outfile);
        }
    }
}
