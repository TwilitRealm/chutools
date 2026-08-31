using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JaiSeqX.JAI.Seq;
using JaiSeqX.JAI.Types;
using JaiSeqX.JAI;
using MidiSharp;
using System.IO;
using System.Diagnostics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace JaiMaker
{
    public partial class MainWindow : Window
    {
        public static readonly DirectProperty<MainWindow, bool> FunctionsEnabledProperty =
            AvaloniaProperty.RegisterDirect<MainWindow, bool>(
                nameof(FunctionsEnabled),
                o => o.FunctionsEnabled,
                (o, v) => o.FunctionsEnabled = v);

        public static readonly DirectProperty<MainWindow, bool> MidiFunctionsEnabledProperty =
            AvaloniaProperty.RegisterDirect<MainWindow, bool>(
                nameof(MidiFunctionsEnabled),
                o => o.MidiFunctionsEnabled,
                (o, v) => o.MidiFunctionsEnabled = v);

        public int[] bankMap = new int[1024];
        public int[] progMap = new int[1024];
        InstrumentBank? currentIBNK;
        Instrument? currentInst;
        MidiSequence? currentSequence;
        // KeysConverter kk;
        public BitArray keysPressed = new(1024);
        string JaiFile = "";
        private bool newBms = false;

        private readonly List<(NumericUpDown Bank, NumericUpDown Program, Button Insert)> _trackControls = [];

        private Dictionary<object, int> btnMap =  new Dictionary<object, int>();
        private Dictionary<int, Dictionary<int, string>>? INAMap;

        public bool FunctionsEnabled { get; set => SetAndRaise(FunctionsEnabledProperty, ref field, value); }
        public bool MidiFunctionsEnabled { get; set => SetAndRaise(MidiFunctionsEnabledProperty, ref field, value); }

        private IDisposable? _updateTimer;

        public MainWindow()
        {
            InitializeComponent();

            for (var i = 0; i < 16; i++)
            {
                TrackGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var block = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
                block.Text = $"Track {i + 1}";
                Grid.SetColumn(block, 0);
                Grid.SetRow(block, i + 1);
                TrackGrid.Children.Add(block);

                var bank = new NumericUpDown
                {
                    IsEnabled = false,
                    Minimum = 0,
                    Maximum = 1024,
                    Value = 0,
                    FormatString = "0"
                };
                Grid.SetColumn(bank, 1);
                Grid.SetRow(bank, i + 1);
                TrackGrid.Children.Add(bank);

                var program = new NumericUpDown
                {
                    IsEnabled = false,
                    Minimum = 0,
                    Maximum = 1024,
                    Value = 0,
                    FormatString = "0"
                };
                Grid.SetColumn(program, 2);
                Grid.SetRow(program, i + 1);
                TrackGrid.Children.Add(program);

                var insertButton = new Button { Content = "Set Selected", IsEnabled = false };
                var channelNum = i;
                Grid.SetColumn(insertButton, 3);
                Grid.SetRow(insertButton, i + 1);
                insertButton.Click += (_, _) =>
                {
                    doInsetChannel(channelNum);
                };
                TrackGrid.Children.Add(insertButton);

                _trackControls.Add((bank, program, insertButton));
            }

            _updateTimer = DispatcherTimer.Run(() =>
            {
                updateTimer_Tick();
                return true;
            }, TimeSpan.FromMilliseconds(100));
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            _updateTimer?.Dispose();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeySymbol == null)
                return;

            e.Handled = (bool)kbmode.IsChecked;

            var sym = char.ToLowerInvariant(e.KeySymbol[0]);

            if (!keysPressed[sym])
            {
                var channel = (byte)sym;
                //Console.WriteLine("key {0}", channel);
                Keyboard.startSound((byte)(channel));
                keysPressed[sym] = true;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeySymbol == null)
                return;

            var sym = char.ToLowerInvariant(e.KeySymbol[0]);

            var channel = (byte)sym;
            Keyboard.stopSound((byte)(channel));
            keysPressed[sym] = false;
        }

        private void EnableFunctions()
        {
            FunctionsEnabled = true;

            UpdateBanks();
        }

        private void UpdateBanks()
        {

            banksList.Items.Clear();
            var bankidx = 0;
            var IBNK = Root.g_AAF.IBNK;
            for (int i = 0; i < IBNK.Length;i++)
            {
                if (IBNK[i]!=null)
                {
                    banksList.Items.Add("Bank " + i);

                    bankMap[bankidx] = i;
                    bankidx++;
                }
            }
        }

        private async void openAAFToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Opening AAF.");
            currentStatus.Text = "Opening AAF";
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open AAF file",
                FileTypeFilter = [
                    new FilePickerFileType("AAF")
                    {
                        Patterns = [ "*.aaf" ]
                    }
                ],
                AllowMultiple = false
            });

            if (files.Count == 0)
            {
                currentStatus.Text = "Opening AAF cancelled";
                return;
            }

            var path = files[0].TryGetLocalPath()!;

            try
            {

                var wtf = new AAFFile();
                wtf.LoadAAFile(path);
                newBms = false;
                JaiFile = path;
                Root.g_AAF = wtf;
                Root.allWSYS = wtf.WSYS;
                currentStatus.Text = "AAF Loaded successfully.";
                EnableFunctions();


            } catch (Exception E)
            {
                var box = MessageBoxManager
                    .GetMessageBoxStandard("ugh", $"Failed opening AAF\n{E}", icon: MsBox.Avalonia.Enums.Icon.Error);

                await box.ShowWindowDialogAsync(this);
            }
        }

        private async void BanksList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                progList.Items.Clear();
                var progidx = 0;
                var IBNK = Root.g_AAF.IBNK;
                if (banksList.SelectedIndex > IBNK.Length || banksList.SelectedIndex > bankMap.Length)
                {
                    return;
                }

                var thisBank = bankMap[banksList.SelectedIndex];
                var CurrentIBNK = IBNK[thisBank];
                currentIBNK = CurrentIBNK;

                Dictionary<int, string> mapOut = null;
                if (INAMap != null)
                {
                    INAMap.TryGetValue(thisBank, out mapOut);
                }

                for (int i = 0; i < CurrentIBNK.Instruments.Length; i++)
                {
                    if (CurrentIBNK.Instruments[i] != null)
                    {

                        string repName = null;

                        if (mapOut != null)
                        {
                            mapOut.TryGetValue(i, out repName);
                        }

                        if (repName != null)
                        {
                            progList.Items.Add((i) + " " + repName);
                        }

                        else if (CurrentIBNK.Instruments[i].IsPercussion)
                        {
                            progList.Items.Add("(PRC)Program " + (i));
                        }
                        else
                        {
                            progList.Items.Add("Program " + (i));
                        }


                        progMap[progidx] = i;
                        progidx++;

                    }
                }

                Root.currentBank = currentIBNK;
            }
            catch (Exception ex)
            {
                var box = MessageBoxManager
                    .GetMessageBoxStandard("ugh", $"Fuck this\n{ex}");

                await box.ShowWindowDialogAsync(this);
            }
        }

        private void updateUIGlobal()
        {
            if (currentSequence!=null)
            {
                MidiFunctionsEnabled = true;

                for (int i=0; i < _trackControls.Count; i++)
                {
                    var (bank, program, insert) = _trackControls[i];

                    var rer = i < currentSequence.Tracks.Count;

                    bank.IsEnabled = rer;
                    program.IsEnabled = rer;
                    insert.IsEnabled = rer;
                }

                /*
                if (File.Exists("JaiSeqX.exe"))
                {
                    launchJSEQ.IsEnabled = true;
                }
                */
            }
            else
            {
                MidiFunctionsEnabled = false;
                //launchJSEQ.IsEnabled = false;
            }
        }

        private void updateChannelData()
        {
            for (int i=0; i < _trackControls.Count; i++)
            {
                var bank = _trackControls[i].Bank;
                var program = _trackControls[i].Program;

                Root.programs[i] = (int)program.Value;
                Root.instrumentBanks[i] = (int)bank.Value;

            }
        }

        private void progList_SelectedIndexChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentIBNK == null) { return; }
                if (progList.SelectedIndex > currentIBNK.Instruments.Length || progList.SelectedIndex > progMap.Length)
                {
                    return;
                }

                currentInst = currentIBNK.Instruments[progMap[progList.SelectedIndex]];
                Root.currentProg = currentInst;
                Root.ProgNumber = progMap[progList.SelectedIndex];
                updateChannelData();
            }
            catch { } // fuck this. I added checks. I cant figure it out

        }

        private void velocityBar_Scroll(object sender, RangeBaseValueChangedEventArgs e)
        {
            Root.currentVel = (int)velocityBar.Value;
        }

        private void keyOffsetBar_Scroll(object sender, RangeBaseValueChangedEventArgs e)
        {
            Root.keyOffset = (int)keyOffsetBar.Value;
        }

        private void updateTimer_Tick(/*object sender, EventArgs e*/)
        {
            updateChannelData();
            updateUIGlobal();
        }

        /*
        private void launchJSEQ_Click(object sender, EventArgs e)
        {
            MidiToBMS.doToBMS(currentSequence, "test.bms");

            var args = string.Format("visu \"{0}\" {1} test.bms",JaiFile,(int)type);
            var b = new ProcessStartInfo("JaiSeqX.exe", args);
            var bw = Process.Start(b);

            bw.WaitForExit();

        }
        */

        private async void exportBMS_Click(object sender, RoutedEventArgs e)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                DefaultExtension = "bms",
                ShowOverwritePrompt = true,
                Title = "Save BMS file"
            });

            if (file == null)
                return;

            await using var fileStream = await file.OpenWriteAsync();
            if (newBms)
            {
                MidiToBMSV2.doToBMS(currentSequence!, fileStream);
            }
            else
            {
                MidiToBMS.doToBMS(currentSequence!, fileStream);
            }
        }

        private static readonly FilePickerOpenOptions OpenMidiOptions = new()
        {
            Title = "Open MIDI file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("MIDI file")
                {
                    AppleUniformTypeIdentifiers = ["public.midi-audio"],
                    MimeTypes = ["audio/midi", "audio/x-midi"],
                    Patterns = ["*.mid", "*.midi", "*.smf"],
                }
            ]
        };

        private async void importMIDIToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(OpenMidiOptions);

                if (files.Count == 0)
                    return;

                await using var b = await files[0].OpenReadAsync();
                currentSequence = MidiSequence.Open(b);
            }
            catch (Exception E)
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Error", "Not a valid midi file");

                await box.ShowWindowDialogAsync(this);
                Console.WriteLine("heck\n{0}", E);
            }
        }

        private async void type1ToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Opening BAA.");
            currentStatus.Text = "Opening BAA";
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open BAA file",
                FileTypeFilter = [
                    new FilePickerFileType("BAA")
                    {
                        Patterns = [ "*.baa" ]
                    }
                ],
                AllowMultiple = false
            });

            if (files.Count == 0)
            {
                currentStatus.Text = "Opening BAA cancelled";
                return;
            }

            var path = files[0].TryGetLocalPath()!;

            try
            {
                var wtf = new BAAFile();
                wtf.LoadBAAFile(path);
                newBms = true;
                JaiFile = path;
                Root.g_AAF = wtf;
                Root.allWSYS = wtf.WSYS;
                currentStatus.Text = "BAA Loaded successfully.";
                EnableFunctions();
            }
            catch (Exception E)
            {
                var box = MessageBoxManager
                    .GetMessageBoxStandard("ugh", $"Failed opening BAA\n{E}", icon: MsBox.Avalonia.Enums.Icon.Error);

                await box.ShowWindowDialogAsync(this);
            }
        }


        private void TempoUpDown_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
        {
            Root.Tempo = (int)TempoUpDown.Value!;
        }

        private void doInsetChannel(int channelNum)
        {
            _trackControls[channelNum].Bank.Value = bankMap[banksList.SelectedIndex];
            _trackControls[channelNum].Program.Value = progMap[progList.SelectedIndex];
        }

        private async void loadINAToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Open INA file",
                    AllowMultiple = false
                });

                if (files.Count == 0)
                    return;

                var path = files[0].TryGetLocalPath()!;

                INAMap = INAFile.parse(path);

                await MessageBoxManager
                    .GetMessageBoxStandard("Success", "INA File loaded successfully.")
                    .ShowWindowDialogAsync(this);
            }
            catch
            {
                var box = MessageBoxManager
                    .GetMessageBoxStandard("Error", "Not a valid INA file", icon: MsBox.Avalonia.Enums.Icon.Error);

                await box.ShowWindowDialogAsync(this);
            }
        }

        private void TwilitRealm_OnClick(object? sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://twilitrealm.dev") { UseShellExecute = true });
        }

        private void Xayr_OnClick(object? sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.xayr.gay/") { UseShellExecute = true });
        }

        private void MenuItem_OnClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
