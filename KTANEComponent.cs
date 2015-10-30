using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
namespace LiveSplit.KTANE {
    public class KTANEComponent : IComponent {
        public string ComponentName { get { return "Keep Talking and Nobody Explodes Autosplitter"; } }
        protected TimerModel Model { get; set; }
        public IDictionary<string, Action> ContextMenuControls { get { return null; } }
        private static string BestTimes = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low", @"Steel Crate Games\Keep Talking and Nobody Explodes\best_times.xml");
        private static string LogFile = null;
        private int currentSplit = 0;
        private decimal bestTime;
        private DateTime startOfBomb, endOfBomb;
        private List<LogLine> logLines = new List<LogLine>();
        private long lastPosition = 0;
        private InfoTextComponent textInfo;

        public KTANEComponent() {
            textInfo = new InfoTextComponent("Best Time", "");
            textInfo.LongestString = "Best Time 123.456 / 123.456";
            textInfo.InformationName = "Best Time";
        }

        public static void Main(string[] args) {
            Process[] ktanes = Process.GetProcessesByName("ktane");
            if (ktanes.Length > 0) {
                LogFile = Path.Combine(Path.GetDirectoryName(ktanes[0].MainModule.FileName), @"logs\ktane.log");
            }
            using (FileStream fs = File.Open(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                long currentLength = fs.Length;
                byte[] data = new byte[currentLength];
                fs.Read(data, 0, data.Length);
                string mytext = Encoding.UTF8.GetString(data);
                Console.WriteLine(mytext);
            }
        }

        private void GetValues(IInvalidator invalidator, LiveSplitState lvstate, float width, float height, LayoutMode mode) {
            if (string.IsNullOrEmpty(LogFile) || !File.Exists(LogFile)) {
                Process[] ktanes = Process.GetProcessesByName("ktane");
                if (ktanes.Length > 0) {
                    LogFile = Path.Combine(Path.GetDirectoryName(ktanes[0].MainModule.FileName), @"logs\ktane.log");
                } else {
                    return;
                }
            }

            if (currentSplit > 0 && currentSplit < 33 && Model != null && Model.CurrentState != null && Model.CurrentState.CurrentPhase == TimerPhase.Running) {
                ReadData();
                foreach (LogLine line in logLines) {
                    if (line.Time < DateTime.Now - Model.CurrentState.CurrentTime.RealTime.Value) {
                        continue;
                    }
                    if (line.Message.IndexOf("[Bomb] A winner is you!!") >= 0) {
                        if (Model.CurrentState.Run.Count < 8) {
                            switch (currentSplit) {
                                case 1:
                                case 5:
                                case 12:
                                case 16:
                                case 22:
                                case 26:
                                case 32:
                                    Model.Split();
                                    break;
                                default:
                                    currentSplit++;
                                    break;
                            }
                        } else {
                            Model.Split();
                        }
                        endOfBomb = line.Time;
                    } else if (line.Message.IndexOf("[BombGenerator] Generating bomb with seed") >= 0) {
                        startOfBomb = line.Time;
                        endOfBomb = DateTime.MinValue;

                        XDocument x = XDocument.Load(BestTimes);
                        var xmlList = x.Descendants("dictionary").Elements().ToList();
                        if (xmlList.Count >= currentSplit) {
                            bestTime = decimal.Parse(xmlList[currentSplit - 1].Element("value").Element("GameRecord").Element("RealTimeElapsed").Value);
                        } else {
                            bestTime = 0;
                        }
                    } else if (line.Message.IndexOf("[Bomb] Boom") >= 0) {
                        startOfBomb = DateTime.MinValue;
                        endOfBomb = DateTime.MinValue;
                    }
                }
            }

            textInfo.InformationValue = (startOfBomb > DateTime.MinValue ? ((endOfBomb > DateTime.MinValue ? endOfBomb : DateTime.Now) - startOfBomb).TotalSeconds.ToString("0.000") : "0.000") + " / " + bestTime.ToString("0.000");
            textInfo.Update(invalidator, lvstate, width, height, mode);
            if (invalidator != null) {
                invalidator.Invalidate(0, 0, width, height);
            }
        }
        private void ReadData() {
            try {
                string text = null;
                using (FileStream fs = File.Open(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    if (fs.Length < lastPosition) {
                        lastPosition = 0;
                    } else {
                        fs.Seek(lastPosition, SeekOrigin.Begin);
                    }
                    text = GetText(fs, lastPosition);
                }

                lastPosition = GenerateData(text, lastPosition);
            } catch (Exception ex) {
                WriteLog(ex.ToString());
            }
        }
        private string GetText(FileStream fs, long position) {
            long currentLength = fs.Length;
            byte[] data = new byte[currentLength - position];
            fs.Read(data, 0, data.Length);
            return Encoding.UTF8.GetString(data);
        }
        private long GenerateData(string text, long lastPosition) {
            logLines.Clear();
            int index = text.IndexOf('\x0A');
            int lastIndex = 0;
            while (index >= 0) {
                string line = text.Substring(lastIndex, index - lastIndex);
                if (line.Length > 30) {
                    string lineType = line.Substring(0, 5);
                    if (lineType == "DEBUG" || lineType == " WARN" || lineType == " INFO") {
                        try {
                            DateTime time = DateTime.Parse(line.Substring(6, 23).Replace(',', '.')).ToLocalTime();
                            string message = line.Substring(30);
                            logLines.Add(new LogLine() { Message = message, Time = time });
                        } catch { }
                    }
                }
                lastIndex = index + 1;
                index = text.IndexOf('\x0A', lastIndex);
            }
            return lastPosition + lastIndex;
        }
        public void Update(IInvalidator invalidator, LiveSplitState lvstate, float width, float height, LayoutMode mode) {
            if (Model == null) {
                Model = new TimerModel() { CurrentState = lvstate };
                lvstate.OnReset += OnReset;
                lvstate.OnPause += OnPause;
                lvstate.OnResume += OnResume;
                lvstate.OnStart += OnStart;
                lvstate.OnSplit += OnSplit;
                lvstate.OnUndoSplit += OnUndoSplit;
                lvstate.OnSkipSplit += OnSkipSplit;
            }

            GetValues(invalidator, lvstate, width, height, mode);
        }

        public void OnReset(object sender, TimerPhase e) {
            currentSplit = 0;
            startOfBomb = DateTime.MinValue;
            endOfBomb = DateTime.MinValue;
            bestTime = 0;
            WriteLog("---------Reset----------------------------------");
        }
        public void OnResume(object sender, EventArgs e) {
            WriteLog("---------Resumed--------------------------------");
        }
        public void OnPause(object sender, EventArgs e) {
            WriteLog("---------Paused---------------------------------");
        }
        public void OnStart(object sender, EventArgs e) {
            currentSplit++;
            WriteLog("---------New Game-------------------------------");
        }
        public void OnUndoSplit(object sender, EventArgs e) {
            currentSplit--;
            bestTime = 0;
            WriteLog(DateTime.Now.ToString(@"HH\:mm\:ss.fff") + " | " + Model.CurrentState.CurrentTime.RealTime.Value.ToString("G").Substring(3, 11) + ": CurrentSplit: " + currentSplit.ToString().PadLeft(24, ' '));
        }
        public void OnSkipSplit(object sender, EventArgs e) {
            currentSplit++;
            WriteLog(DateTime.Now.ToString(@"HH\:mm\:ss.fff") + " | " + Model.CurrentState.CurrentTime.RealTime.Value.ToString("G").Substring(3, 11) + ": CurrentSplit: " + currentSplit.ToString().PadLeft(24, ' '));
        }
        public void OnSplit(object sender, EventArgs e) {
            currentSplit++;
            WriteLog(DateTime.Now.ToString(@"HH\:mm\:ss.fff") + " | " + Model.CurrentState.CurrentTime.RealTime.Value.ToString("G").Substring(3, 11) + ": CurrentSplit: " + currentSplit.ToString().PadLeft(24, ' '));
        }
        private void WriteLog(string data) {
            //using (StreamWriter wr = new StreamWriter("_APixelStory.log", true)) {
            //    wr.WriteLine(data);
            //}
        }

        public Control GetSettingsControl(LayoutMode mode) { return null; }
        public void SetSettings(XmlNode settings) { }
        public XmlNode GetSettings(XmlDocument document) { return null; }
        public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion) {
            if (state.LayoutSettings.BackgroundColor.ToArgb() != Color.Transparent.ToArgb()) {
                g.FillRectangle(new SolidBrush(state.LayoutSettings.BackgroundColor), 0, 0, HorizontalWidth, height);
            }
            PrepareDraw(state, LayoutMode.Horizontal);
            textInfo.DrawHorizontal(g, state, height, clipRegion);
        }
        public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion) {
            if (state.LayoutSettings.BackgroundColor.ToArgb() != Color.Transparent.ToArgb()) {
                g.FillRectangle(new SolidBrush(state.LayoutSettings.BackgroundColor), 0, 0, width, VerticalHeight);
            }
            PrepareDraw(state, LayoutMode.Vertical);
            textInfo.DrawVertical(g, state, width, clipRegion);
        }
        private void PrepareDraw(LiveSplitState state, LayoutMode mode) {
            textInfo.DisplayTwoRows = false;

            textInfo.NameLabel.HasShadow = textInfo.ValueLabel.HasShadow = state.LayoutSettings.DropShadows;
            textInfo.NameLabel.HorizontalAlignment = StringAlignment.Near;
            textInfo.ValueLabel.HorizontalAlignment = StringAlignment.Far;
            textInfo.NameLabel.VerticalAlignment = StringAlignment.Near;
            textInfo.ValueLabel.VerticalAlignment = StringAlignment.Far;
            textInfo.NameLabel.ForeColor = state.LayoutSettings.TextColor;
            textInfo.ValueLabel.ForeColor = state.LayoutSettings.TextColor;
        }
        public float HorizontalWidth { get { return textInfo.HorizontalWidth; } }
        public float VerticalHeight { get { return textInfo.VerticalHeight; } }
        public float MinimumHeight { get { return textInfo.MinimumHeight; } }
        public float MinimumWidth { get { return textInfo.MinimumWidth; } }
        public float PaddingBottom { get { return textInfo.PaddingBottom; } }
        public float PaddingLeft { get { return textInfo.PaddingLeft; } }
        public float PaddingRight { get { return textInfo.PaddingRight; } }
        public float PaddingTop { get { return textInfo.PaddingTop; } }
        public void Dispose() { }
    }
    public class LogLine {
        public DateTime Time;
        public string Message;

        public override string ToString() {
            return Time.ToLongDateString() + " - " + Message;
        }
    }
}