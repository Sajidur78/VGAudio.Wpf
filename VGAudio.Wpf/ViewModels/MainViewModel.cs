using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Ookii.Dialogs.Wpf;
using VGAudio.Containers.Dsp;
using VGAudio.Formats;
using VGAudio.Containers;
using VGAudio.Containers.Adx;
using VGAudio.Containers.Idsp;
using VGAudio.Containers.NintendoWare;
using VGAudio.Wpf.Annotations;
using VGAudio.Wpf.Audio;

namespace VGAudio.Wpf.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public MainState State { get; set; } = MainState.NotOpened;

        //Hacky solution due to weird issues in UWP
        //Binding problems with Dictionary<enum, object> but not Dictionary<int, object>
        public Dictionary<int, ContainerType> FileTypesBinding { get; }
        public int SelectedFileTypeBinding
        {
            get => (int)SelectedFileType;
            set => SelectedFileType = (FileType)value;
        }

        public Dictionary<NwCodec, string> FormatTypesBinding { get; } = new Dictionary<NwCodec, string>
        {
            [NwCodec.GcAdpcm] = "DSP-ADPCM",
            [NwCodec.Pcm16Bit] = "16-bit PCM",
            [NwCodec.Pcm8Bit] = "8-bit PCM"
        };

        public FileType SelectedFileType { get; set; } = FileType.Dsp;

        public bool StateNotOpened => State == MainState.NotOpened;
        public bool StateOpened => State == MainState.Opened;
        public bool StateSaved => State == MainState.Saved;

        public double Time { get; set; }
        public double Samples { get; set; }
        public double SamplesPerMs => Samples / (Time * 1000);

        public int BatchProgress { get; set; }

        public DspConfiguration DspConfiguration { get; set; } = new DspConfiguration();
        public BxstmConfiguration BxstmConfiguration { get; set; } = new BxstmConfiguration();
        public IdspConfiguration IdspConfiguration { get; set; } = new IdspConfiguration();
        public AdxConfiguration AdxConfiguration { get; set; } = new AdxConfiguration();

        public string InPath { get; set; }
        public double InSize { get; set; }

        public double InSampleRate { get; set; }

        public TimeSpan InDuration { get; set; }

        public AudioData AudioData { get; set; }
        public bool Looping { get; set; }
        public int LoopStart { get; set; }
        public int LoopEnd { get; set; }

        public ObservableCollection<string> InFiles { get; set; } = new ObservableCollection<string>();
        public string SelectedFilePath { get; set; }
        public AudioInfo SelectedFile { get; set; }

        public RelayCommand SaveFileCommand { get; }
        public RelayCommand OpenFileCommand { get; }

        public RelayCommand AddFileCommand { get; }

        public RelayCommand RemoveFileCommand { get; }

        public RelayCommand ClearFilesCommand { get; }

        public RelayCommand BatchSaveCommand { get; }

        public bool Saving { get; private set; } = false;

        public MainViewModel()
        {
            FileTypesBinding = AudioInfo.Containers.Where(x => x.Value.GetWriter != null).ToDictionary(x => (int)x.Key, x => x.Value);

            SaveFileCommand = new RelayCommand(SaveFile, CanSave);
            OpenFileCommand = new RelayCommand(OpenFile);
            AddFileCommand = new RelayCommand(AddFile);
            RemoveFileCommand = new RelayCommand(RemoveFile, () => SelectedFilePath != null && !Saving);
            ClearFilesCommand = new RelayCommand(() => InFiles.Clear(), () => InFiles.Count != 0 && !Saving);
            BatchSaveCommand = new RelayCommand(BatchSaveFile, () => InFiles.Count != 0 && !Saving);
        }

        private bool CanSave() => AudioData != null && !Saving;

        private void AddFile()
        {
            try
            {
                var results = OpenFilePicker(true);

                if (results is null)
                    return;

                ClearFilesCommand.RaiseCanExecuteChanged();
                RemoveFileCommand.RaiseCanExecuteChanged();
                BatchSaveCommand.RaiseCanExecuteChanged();

                foreach (var file in results)
                {
                    InFiles.Add(file);
                }
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show(ex.Message, "Unable to parse file");
            }
        }

        public void RemoveFile()
        {
            InFiles.Remove(SelectedFilePath);
        }

        private void OpenFile()
        {
            try
            {
                var info = PickFile();

                if(info is null)
                    return;

                InSize = ((double)new FileInfo(info.FileName).Length) / (1024 * 1024);
                InSampleRate = (double)info.AudioFormat.SampleRate / 10000;
                InDuration = TimeSpan.FromSeconds((double)info.AudioFormat.SampleCount / info.AudioFormat.SampleRate);

                InPath = Path.GetFileName(info.FileName);
                LoopStart = info.AudioFormat.LoopStart;
                LoopEnd = info.AudioFormat.LoopEnd;
                Looping = info.AudioFormat.Looping;

                AudioData = new AudioData(info.AudioFormat);
                State = MainState.Opened;
                Time = 0;
                SaveFileCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show(ex.Message, "Unable to parse file");
            }
        }

        private async void SaveFile()
        {
            var savePicker = new VistaSaveFileDialog()
            {
                FileName = Path.ChangeExtension(Path.GetFileName(InPath), "." + AudioInfo.Containers[SelectedFileType].Extensions.First()),
                Filter = $"{AudioInfo.Containers[SelectedFileType].Description}|*.{AudioInfo.Containers[SelectedFileType].Extensions.First()}"
            };

            Saving = true;
            try
            {
                if (!savePicker.ShowDialog().Value) return;

                if (Looping)
                {
                    AudioData.SetLoop(Looping, LoopStart, LoopEnd);
                }

                var watch = new Stopwatch();

                byte[] file = null;
                await Task.Run(() =>
                {
                    watch.Start();
                    file = AudioInfo.Containers[SelectedFileType].GetWriter().GetFile(AudioData, GetConfiguration(SelectedFileType));
                    watch.Stop();
                });

                Time = watch.Elapsed.TotalSeconds;
                Samples = AudioData.GetAllFormats().First().SampleCount;

                File.WriteAllBytes(savePicker.FileName, file);
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show(ex.Message, "Error writing file");
            }
            finally
            {
                Saving = false;
            }
        }

        private async void BatchSaveFile()
        {
            var dialog = new VistaFolderBrowserDialog() { ShowNewFolderButton = true};
            
            if(!dialog.ShowDialog().Value)
                return;

            BatchProgress = 0;
            Saving = true;
            var path = dialog.SelectedPath;
            var errors = new List<string>();

            try
            {
                await Task.Run(() =>
                {
                    var watch = new Stopwatch();
                    watch.Start();
                    Parallel.ForEach(InFiles, file =>
                    {
                        Saving = true;
                        if (File.Exists(file))
                        {
                            try
                            {
                                string GetFileName(string filePath)
                                {
                                    var newName = Path.ChangeExtension(Path.GetFileName(filePath),
                                        $".{AudioInfo.Containers[SelectedFileType].Extensions.First()}");
                                    var newPath = Path.Combine(path, newName);

                                    if (File.Exists(newPath))
                                        newPath = Path.Combine(path,
                                            $"{Path.GetFileNameWithoutExtension(filePath)}_{InFiles.IndexOf(filePath)}.{AudioInfo.Containers[SelectedFileType].Extensions.First()}");

                                    return newPath;
                                }

                                var source = IO.OpenFile(file);
                                using (var stream = File.Create(GetFileName(file)))
                                {
                                    AudioInfo.Containers[SelectedFileType].GetWriter().WriteToStream(source, stream,
                                        GetConfiguration(SelectedFileType));
                                }

                                source = null;
                            }
                            catch (Exception e)
                            {
                                errors.Add($"{file}: {e.Message}");
                            }
                        }

                        GC.Collect();
                        BatchProgress++;
                    });
                    watch.Stop();
                    HandyControl.Controls.MessageBox.Show($@"Finished encoding in {watch.Elapsed.TotalSeconds}.
Errors: {errors.Count}
{string.Join("\r\n", errors)}", "VGAudio");
                    Saving = false;
                });
            }
            catch (Exception e)
            {
                HandyControl.Controls.MessageBox.Show(e.Message, "Error writing file");
            }
            finally
            {
                Saving = false;
            }
        }

        private string[] OpenFilePicker(bool multiSelect = false)
        {
            var picker = new VistaOpenFileDialog() { Multiselect = multiSelect };
            var builder = new StringBuilder();
            builder.Append("All Files|");
            foreach (var extension in AudioInfo.Extensions.Keys)
            {
                builder.Append($"*.{extension}; ");
            }

            foreach (var container in AudioInfo.Containers.Values)
            {
                builder.Append($"|{container.Description}|*.{container.Extensions.First()}");
            }

            picker.Filter = builder.ToString();

            if (!picker.ShowDialog().Value) return null;

            return picker.FileNames;
        }

        private AudioInfo PickFile()
        {
            var files = OpenFilePicker();

            if (files is null) return null;

            return new AudioInfo(files[0], IO.OpenFile(files[0]));
        }

        private Configuration GetConfiguration(FileType type)
        {
            switch (type)
            {
                case FileType.Dsp:
                    return DspConfiguration;
                case FileType.Idsp:
                    return IdspConfiguration;
                case FileType.Brstm:
                case FileType.Bcstm:
                case FileType.Bfstm:
                    return BxstmConfiguration;
                case FileType.Adx:
                    return AdxConfiguration;
                default:
                    return null;
            }
        }

        public enum MainState
        {
            NotOpened,
            Opened,
            Saved
        }
    }

    public class RelayCommand : ICommand
    {
        private Action mExecute;
        private Func<bool> mCanExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            mExecute = execute;
            mCanExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return mExecute == null || mCanExecute == null ? true : mCanExecute();
        }

        public void Execute(object parameter)
        {
            mExecute();
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
