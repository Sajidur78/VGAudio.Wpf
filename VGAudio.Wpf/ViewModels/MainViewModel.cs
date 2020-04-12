using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
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

        public DspConfiguration DspConfiguration { get; set; } = new DspConfiguration();
        public BxstmConfiguration BxstmConfiguration { get; set; } = new BxstmConfiguration();
        public IdspConfiguration IdspConfiguration { get; set; } = new IdspConfiguration();
        public AdxConfiguration AdxConfiguration { get; set; } = new AdxConfiguration();

        public string InPath { get; set; }
        private IAudioFormat InFormat { get; set; }

        public AudioData AudioData { get; set; }
        public bool Looping { get; set; }
        public int LoopStart { get; set; }
        public int LoopEnd { get; set; }

        public RelayCommand SaveFileCommand { get; }
        public ICommand OpenFileCommand { get; }
        private bool Saving { get; set; }

        public MainViewModel()
        {
            FileTypesBinding = AudioInfo.Containers.Where(x => x.Value.GetWriter != null).ToDictionary(x => (int)x.Key, x => x.Value);

            SaveFileCommand = new RelayCommand(SaveFile, CanSave);
            OpenFileCommand = new RelayCommand(OpenFile);
        }

        private bool CanSave() => AudioData != null && !Saving;

        private async void OpenFile()
        {
            var picker = new OpenFileDialog();
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

            if (!picker.ShowDialog().Value) return;

            try
            {
                InPath = picker.FileName;

                InFormat = await Task.Run(() => IO.OpenFile(picker.FileName));
                IAudioFormat format = InFormat;

                LoopStart = format.LoopStart;
                LoopEnd = format.LoopEnd;
                Looping = format.Looping;

                AudioData = new AudioData(format);
                State = MainState.Opened;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to parse file");
            }
        }

        private async void SaveFile()
        {
            var savePicker = new SaveFileDialog()
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
                MessageBox.Show(ex.Message, "Error writing file");
            }
            finally
            {
                Saving = false;
            }
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
    }
}
