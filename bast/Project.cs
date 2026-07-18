using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Be.IO;

namespace xayrga.bast
{
    public class JASEProject
    {
        public int version;
        public int revision;
        public string format = "JASE";
        public string[] includes;       
    }

    public class JBSTProject
    {
        public int version;
        public int revision;
        public string format = "BST";
        public string[] includes;

       public static JBST loadFile(string folder)
        {
            var data = File.ReadAllText($"{folder}/root.json");
            var projectRoot = folder;


            JBSTProject project = JsonConvert.DeserializeObject<JBSTProject>(data);

            JBST outBST = new JBST();
            outBST.categories = new JBSTCategory[project.includes.Length];


            for (int i = 0; i < project.includes.Length; i++)
            {
                var incAbsPath = $"{projectRoot}/{project.includes[i]}/category.json";
                var l = File.ReadAllText(incAbsPath);
                var catPre = JsonConvert.DeserializeObject<JBSTCategoryProject>(l);
                var cat = new JBSTCategory();
                cat.name = catPre.name;
                cat.libraries = new JBSTLibrary[catPre.includes.Length];
                outBST.categories[i] = cat;

                for (int q = 0; q < catPre.includes.Length; q++)
                {
                    var incLibAbs = $"{Path.GetDirectoryName(incAbsPath)}/{catPre.includes[q]}";
                    var incLibData = File.ReadAllText(incLibAbs);
                    var libData = JsonConvert.DeserializeObject<JToken>(incLibData);
                    var lib = new JBSTLibrary();
                    lib.name = (string)libData["name"];
                    var libSoundData = libData["sounds"];
                    var lc = libSoundData.Children().Count();
                    lib.sounds = new JBSTSound[lc];
                    for (int b=0; b < lc; b++)
                    {
                        var sndDataJSON = libSoundData[b];
                        JBSTSound sound = null; 
                        if (sndDataJSON==null || !sndDataJSON.HasValues)
                        {
                            lib.sounds[b] = null;
                            continue;
                        }
                        //Console.WriteLine(b);
                        var stIdent = (int)sndDataJSON["type"];
                        switch (stIdent)
                        {
                            case 0x50:
                                sound = JsonConvert.DeserializeObject<JBSTSeSound>(sndDataJSON.ToString());
                                break;
                            case 0x51:
                                sound = JsonConvert.DeserializeObject<JBSTExtendedSeSound>(sndDataJSON.ToString());
                                break;
                            case 0x60:
                                sound = JsonConvert.DeserializeObject<JBSTSequenceEntry>(sndDataJSON.ToString());
                                break;
                            
                            case 0x70:
                            case 0x71:
                                sound = JsonConvert.DeserializeObject<JBSTStreamEntry>(sndDataJSON.ToString());
                                break;
                        }
                        lib.sounds[b] = sound;
                    }

                    cat.libraries[q] = lib;                   
                }           
            }



            return outBST;
        }
    }

    public class JBSTCategoryProject
    {
        public string name;
        public string[] includes;
    }
}
