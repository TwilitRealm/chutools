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
    class JBSTPacker
    {

        private const int HEAD_BSTN = 0x4253544E;
        private const int HEAD_BST = 0x42535420;

        private Stream streamBST;
        private BeBinaryWriter writerBST;

        public JBST bst;

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
            writerBST.Write(HEAD_BST);
            writerBST.Write(0); // 0 
            writerBST.Write(0x01000000);
            writerBST.Write(0x20);
            for (int i = 0; i < 4; i++)
                writerBST.Write(0);
            prewriteHeader();
            prewriteCategories();
            prewriteLibraries();
            fillSounds();
            writerBST.Flush();


            fillHeader();
            fillCategories();
            writeLibaries();
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
            for (int i=0; i < bst.categories.Length; i++)
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
        
        private void fillSounds()
        {
            for (int i = 0; i < bst.categories.Length; i++)
            {
                var currentCat = bst.categories[i];
                for (int lib = 0; lib < currentCat.libraries.Length; lib++)
                {
                    var currentLib = currentCat.libraries[lib];
                    for (int snd = 0; snd < currentLib.sounds.Length; snd++)
                    {
                        var currentSnd = currentLib.sounds[snd];
                        packSoundType(currentSnd);
                    }
                }
            }

            writerBST.BaseStream.Flush();
            writerBST.BaseStream.Position = writerBST.BaseStream.Length;

            for (int i = 0; i < bst.categories.Length; i++)
            {
                var currentCat = bst.categories[i];
                for (int lib = 0; lib < currentCat.libraries.Length; lib++)
                {
                    var currentLib = currentCat.libraries[lib];
                    for (int snd = 0; snd < currentLib.sounds.Length; snd++)
                    {
                        var currentSnd = currentLib.sounds[snd];
                        packExtendedData(currentSnd);
                    }
                }
            }
            writerBST.BaseStream.Flush();
            for (int i = 0; i < bst.categories.Length; i++)
            {
                var currentCat = bst.categories[i];
                for (int lib = 0; lib < currentCat.libraries.Length; lib++)
                {
                    var currentLib = currentCat.libraries[lib];
                    for (int snd = 0; snd < currentLib.sounds.Length; snd++)
                    {
                        var currentSnd = currentLib.sounds[snd];
                        flushExtendedData(currentSnd);
                    }
                }
            }
        }



        private void packSoundType(JBSTSound snd)
        {
            if (snd==null)
            {
                return;
            }
            snd.mOffset = (int)writerBST.BaseStream.Position;
            var lowerType = snd.type;
            switch (lowerType)
            {
                case 0x00:
                    break;
                case 0x50:
                    var q = ((JBSTSeSound)snd);
                    q.serialize(writerBST);
                    break;
                case 0x51:
                    var q2 = (JBSTExtendedSeSound)snd;
                    q2.serialize(writerBST);
                    break;
                case 0x60:
                    var w = ((JBSTSequenceEntry)snd);
                    w.serialize(writerBST);
                    break;
                case 0x71:
                case 0x70:
                    var e = ((JBSTStreamEntry)snd);
                    e.serialize(writerBST);
                    break;
            }
        }

        private void packExtendedData(JBSTSound snd)
        {
            if (snd == null)
                return;
            var lowerType = snd.type >> 4;
            switch (lowerType)
            {
                case 0x7:
                    var e = ((JBSTStreamEntry)snd);
                    e.pathOffset = (int)writerBST.BaseStream.Position;
                    writeCTerminatedString(writerBST, e.streamPath);                    
                    break;
            }

        }

        private void flushExtendedData(JBSTSound snd)
        {
            if (snd == null)
                return;
            var lowerType = snd.type >> 4;
            switch (lowerType)
            {
                case 0x7:
                    var e = ((JBSTStreamEntry)snd);
                    writerBST.BaseStream.Position = e.mOffset + 4;
                    writerBST.Write(e.pathOffset);
                    break;
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
                    writerBST.Write(0); // Not needed for BST, only BSTN, name pointer.
                    for (int snd = 0; snd < currentLib.sounds.Length; snd++)
                    {
                        var currentSnd = currentLib.sounds[snd];
                        if (currentSnd != null)
                        {
                            var sTypeAndPointer = (currentSnd.type << 24) | (currentSnd.mOffset & 0xFFFFFF);
                            writerBST.Write(sTypeAndPointer);
                        } else
                        {
                            writerBST.Write(0);
                        }
                    }
                }
            }
        }
    }
}
