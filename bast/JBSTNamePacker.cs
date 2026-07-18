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
    public class JBSTNamePacker
    {

        private const int HEAD_BSTN = 0x4253544E;

        private Stream streamBST;
        private BeBinaryWriter writerBST;


        public JBST bst;

        public int libraryNameOffsets;

        public void doPack(string projectDir, string file)
        {
            // cmdarg.assert(!File.Exists($"{projectDir}/root.json"), $"File {file} does not exist");
            if (File.Exists(file))
                File.Delete(file);

            streamBST = File.Open(file, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            writerBST = new BeBinaryWriter(streamBST);

            // unpackJBST(projectDir);
        }

        public void packJBST()
        {

            writerBST.Write(HEAD_BSTN);
            writerBST.Write(0); // 0 
            writerBST.Write(0x01000000);
            writerBST.Write(0x20);
            for (int i = 0; i < 4; i++)
                writerBST.Write(0);
            prewriteHeader();
            prewriteCategories();
            prewriteLibraries();
            writerBST.Flush();
            writeStrings();

            fillHeader();
            fillCategories();
            writeLibaries();
            writerBST.Flush();

        }



        private void writeStrings()
        {
            for (int i = 0; i < bst.categories.Length; i++)
            {
                var currentCat = bst.categories[i];
                currentCat.NameOffset= (int)writerBST.BaseStream.Position;
                writeCTerminatedString(writerBST, currentCat.name);
                for (int lib = 0; lib < currentCat.libraries.Length; lib++)
                {

                    var currentLib = currentCat.libraries[lib];
                    currentLib.NameOffset = (int)writerBST.BaseStream.Position;
                    writeCTerminatedString(writerBST, currentLib.name);
                    for (int snd = 0; snd < currentLib.sounds.Length; snd++)
                    {
                        var currentSnd = currentLib.sounds[snd];
                        if (currentSnd != null)
                        {
                            currentSnd.NameOffset = (int)writerBST.BaseStream.Position;
                            writeCTerminatedString(writerBST, currentSnd.name);
                        } else
                        {
                            writerBST.Write((short)0);
                        }
                    }
                }
            }
        }
        private void writeCTerminatedString(BeBinaryWriter stream, string data)
        {
            var sdata = Encoding.ASCII.GetBytes(data);
            stream.Write(sdata);
            stream.Write((byte)0x00);
        }
        private void prewriteHeader()
        {
            writerBST.Write(bst.categories.Length); // Write the length of the categories
            for (int i = 0; i < bst.categories.Length; i++)
            {
                var currentCat = bst.categories[i];

                writerBST.Write(1); // Pointer template (4 bytes) 
            }
        }
        private void prewriteCategories()
        {
            for (int i = 0; i < bst.categories.Length; i++)
            {
                var currentCat = bst.categories[i];
                currentCat.mOffset = (int)writerBST.BaseStream.Position; // Store category position
                writerBST.Write(currentCat.libraries.Length);
                writerBST.Write(0);
                for (int lib = 0; lib < currentCat.libraries.Length; lib++)
                {
                    var currentLib = currentCat.libraries[lib];
                    writerBST.Write(02);
                }
            }
        }

        private void prewriteLibraries()
        {
            for (int i = 0; i < bst.categories.Length; i++)
            {
                var currentCat = bst.categories[i];
                for (int lib = 0; lib < currentCat.libraries.Length; lib++)
                {
                    var currentLib = currentCat.libraries[lib];
                    currentLib.mOffset = (int)writerBST.BaseStream.Position;
                    writerBST.Write(currentLib.sounds.Length);
                    writerBST.Write(0); // Not needed for BST, only BSTN, name pointer.
                    for (int snd = 0; snd < currentLib.sounds.Length; snd++)
                    {
                        var currentSnd = currentLib.sounds[snd];
                        writerBST.Write(03);
                    }
                }
            }
        }

        private void fillHeader()
        {
            writerBST.BaseStream.Position = 0x20;
            writerBST.Write(bst.categories.Length); // Write the length of the categories
          
            for (int i = 0; i < bst.categories.Length; i++)
            {
                var currentCat = bst.categories[i];
                writerBST.Write(currentCat.mOffset); // now write the transformed category offset. 
            }
        }
        private void fillCategories()
        {
            for (int i = 0; i < bst.categories.Length; i++)
            {
                var currentCat = bst.categories[i];
           
                writerBST.Write(currentCat.libraries.Length);
                writerBST.Write(currentCat.NameOffset);
                for (int lib = 0; lib < currentCat.libraries.Length; lib++)
                {
                    var currentLib = currentCat.libraries[lib];
                    writerBST.Write(currentLib.mOffset);
                }
            }
        }

        private void writeLibaries()
        {
            for (int i = 0; i < bst.categories.Length; i++)
            {
                var currentCat = bst.categories[i];
                for (int lib = 0; lib < currentCat.libraries.Length; lib++)
                {
                    var currentLib = currentCat.libraries[lib];
                    writerBST.BaseStream.Position = currentLib.mOffset;
                    writerBST.Write(currentLib.sounds.Length);
                    writerBST.Write(currentLib.NameOffset);
                    for (int snd = 0; snd < currentLib.sounds.Length; snd++)
                    {
                        var currentSnd = currentLib.sounds[snd];
                        if (currentSnd != null)
                            writerBST.Write(currentSnd.NameOffset);
                        else
                            writerBST.Write(0);

                    }
                }
            }
        }
    }
}
