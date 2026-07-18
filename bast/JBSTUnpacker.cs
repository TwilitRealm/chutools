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
    class JBSTUnpacker
    {

        private Stream streamBST;
        private BeBinaryReader readerBST;

        private Stream streamBSTN;
        private BeBinaryReader readerBSTN; 

        public void doUnpack(string file,string BSTNFile,  string projectDir)
        {
            cmdarg.assert(!File.Exists(file), $"File {file} does not exist");
            cmdarg.assert(!File.Exists(BSTNFile), $"File {BSTNFile} does not exist");

            streamBST = File.Open(file, FileMode.Open);
            readerBST = new BeBinaryReader(streamBST);

            streamBSTN = File.Open(BSTNFile, FileMode.Open);
            readerBSTN = new BeBinaryReader(streamBSTN);

            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory($"{projectDir}/includes");

            unpackJBST(projectDir);
        }

        private void unpackJBST(string projectDir)
        {
            JBST bst = JBST.fromStream(readerBST);
            JBST bstn = JBST.fromStream(readerBSTN);

            JBSTParseUtil.mergeJBST_JBSTN(bst, bstn); // load names and categories 

            var outDir = projectDir;
            string[] categoryPaths = new string[bst.categories.Length];
            for (int categoryIndex = 0; categoryIndex < categoryPaths.Length; categoryIndex++)
            {
                var currentCat = bst.categories[categoryIndex];
                categoryPaths[categoryIndex] = $"{currentCat.name}";
                Directory.CreateDirectory($"{outDir}/{currentCat.name}/libraries");

                var libraryPaths = new string[currentCat.libraries.Length];
                for (int libraryIndex = 0; libraryIndex < currentCat.libraries.Length; libraryIndex++)
                {
                    var currentLib = currentCat.libraries[libraryIndex];
                    libraryPaths[libraryIndex] = $"libraries/{currentLib.name}.json";
                    File.WriteAllText($"{outDir}/{currentCat.name}/libraries/{currentLib.name}.json", JsonConvert.SerializeObject(currentLib, Formatting.Indented));
                }
                File.WriteAllText($"{outDir}/{currentCat.name}/category.json", JsonConvert.SerializeObject(new JBSTCategoryProject() { name = currentCat.name, includes = libraryPaths }, Formatting.Indented));
            }

            var project = new JBSTProject()
            {
                includes = categoryPaths,
                version = bst.version,
                revision = 1,
            };
            File.WriteAllText($"{outDir}/root.json", JsonConvert.SerializeObject(project, Formatting.Indented));
        }

    }
}
