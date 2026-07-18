using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Be.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace xayrga.bast
{
    class Program
    {

        static void Main(string[] args)
        {
            //args = new string[] { "unpackbst" ,"0.bst" ,"1.bstn" ,"TestOut"};




#if DEBUG

            Console.ForegroundColor = ConsoleColor.Red ;
            Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            Console.WriteLine("!BAST build in debug mode, do not push into release!");
            Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            Console.ForegroundColor = ConsoleColor.Gray;

            args = new string[] { 
            "unpackbst",
            "0.bst",
            "1.bstn",
            "MKDDOut"
            };
           
            /*
            var w = File.OpenRead("1.bstn");
            var k = new BeBinaryReader(w);
            var bstn = JBST.fromStream(k);
            Console.WriteLine("BSTN parse successful!");
             w = File.OpenRead("0.bst");
             k = new BeBinaryReader(w);
             var bst = JBST.fromStream(k);
             Console.WriteLine("BST Parse successful!");
            JBSTParseUtil.mergeJBST_JBSTN(bst, bstn);
            //File.WriteAllText("help_me_i_need_help.json",Newtonsoft.Json.JsonConvert.SerializeObject(bst,Newtonsoft.Json.Formatting.Indented));

            Console.ReadLine();
            return;
            */
            /*
          var w = File.OpenRead("1.bstn");
          var k = new BeBinaryReader(w);
          var bstn = JBST.fromStream(k);
          Console.WriteLine("BSTN parse successful!");
          w = File.OpenRead("0.bst");
          k = new BeBinaryReader(w);
          var bst = JBST.fromStream(k);
          Console.WriteLine("BST Parse successful!");
          JBSTParseUtil.mergeJBST_JBSTN(bst, bstn);



          var repackerTest = new JBSTPacker();
          repackerTest.doPack("TestOut", "testOut.bst");
          repackerTest.bst = bst;
          repackerTest.packJBST();

          var bstnTest = new JBSTNamePacker();
         bstnTest.doPack("TestOut", "testOut.bstn");
         bstnTest.bst = bst;
          bstnTest.packJBST();
         */
            //return;
           // return;
#endif 

            cmdarg.cmdargs = args; // load command line argument system
            var operation = cmdarg.assertArg(0, "Operation");
            switch (operation)
            {
                case "unpack":
                    {
                        var inFile = cmdarg.assertArg(1, "Input File");
                        var projDir = cmdarg.assertArg(2, "Project Folder");
                        var unpk = new JASEUnpacker();
                        unpk.doUnpack(inFile, projDir);
                    }
                    break;
                case "pack":
                    {
                        var outFile = cmdarg.assertArg(2, "Output File");
                        var projDir = cmdarg.assertArg(1, "Project Folder");
                        var packOrder = cmdarg.tryArg(3,"! PackOrder is not specified and is highly recommended.");
                        if (packOrder == null)
                            packOrder = "guess";
                        var pck = new JASEPacker();
                        pck.doPack(projDir, outFile,packOrder);
                    }
                    break;
                case "unpackbst":
                    {
                        var inFile = cmdarg.assertArg(1, "Input BST File");
                        var inFileBSTN = cmdarg.assertArg(2, "Input BSTN File");
                        var projDir = cmdarg.assertArg(3, "Project Folder");
                        var unpk = new JBSTUnpacker();
                        unpk.doUnpack(inFile, inFileBSTN, projDir);
                    }
                    break;
                case "packbst":
                    {
                        var projDir = cmdarg.assertArg(1, "Project Folder");
                        var outFileBST = cmdarg.assertArg(2, "output BST File");
                        var outFileBSTN = cmdarg.assertArg(3, "output BSTN File");

                        JBST BST = null;
                        try
                        {
                            BST = JBSTProject.loadFile(projDir);
                        } catch (Exception E)
                        {

                            cmdarg.assert($"Can't load your project file!\n {E.ToString()}");
                        }

                        var repackerTest = new JBSTPacker();
                        repackerTest.doPack("TestOut", outFileBST); // pack out into test22bst
                        repackerTest.bst = BST;
                        repackerTest.packJBST();

                        var bstnTest = new JBSTNamePacker();
                        bstnTest.doPack("TestOut", outFileBSTN); // pack out into test22bstn
                        bstnTest.bst = BST;
                        bstnTest.packJBST();
                    }
                    break;
                case "help":
                    printHelp();
                    break;
                default:
                    Console.WriteLine("Unknown operation, try 'bast help'");
                    break;
            }
        
        }

        static void printHelp()
        {
            Console.WriteLine("bast");
            Console.WriteLine("syntax:");
            Console.WriteLine();
            Console.WriteLine("bast unpack <bst file> <output folder>");
            Console.WriteLine("bast pack <project folder> <bst file>");
            Console.WriteLine("bast unpackbst <bst file> <bstn file> <output folder>");
            Console.WriteLine("bast packbst <project folder> <bst file> <bstn file>");
        }
    }
}
 