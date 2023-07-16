﻿using Maize;
using Maize.Services;
using Maize.Helpers;
using ReactiveUI;
using System.Diagnostics;
using System.Reactive;
using Maize.Models.ApplicationSpecific;
using System.Text;
using Maize.Models.Responses;

namespace MaizeUI.ViewModels
{
    public class ScriptingCryptoAirdropInputFileWindowViewModel : ViewModelBase
    {
        public string Notice { get; set; }
        public Constants.Environment environment;
        public Constants.Environment Environment
        {
            get => environment;
            set => this.RaiseAndSetIfChanged(ref environment, value);
        }
        public string nftData;
        public string NftData
        {
            get => nftData;
            set => this.RaiseAndSetIfChanged(ref nftData, value?.Trim());
        }
        public string cryptoAmount;
        public string CryptoAmount
        {
            get => cryptoAmount;
            set => this.RaiseAndSetIfChanged(ref cryptoAmount, value);
        }
        public string memo;
        public string Memo
        {
            get => memo;
            set => this.RaiseAndSetIfChanged(ref memo, value);
        }

        public bool isEnabled = true;

        public bool IsEnabled
        {
            get => isEnabled;
            set => this.RaiseAndSetIfChanged(ref isEnabled, value);
        }

        public string log;

        public string Log
        {
            get => log;
            set => this.RaiseAndSetIfChanged(ref log, value);
        }

        public LoopringServiceUI loopringService;

        public LoopringServiceUI LoopringService
        {
            get => loopringService;
            set => this.RaiseAndSetIfChanged(ref loopringService, value);
        }

        public Settings settings;

        public Settings Settings
        {
            get => settings;
            set => this.RaiseAndSetIfChanged(ref settings, value);
        }

        public ReactiveCommand<Unit, Unit> CreateInputFileCommand { get; }

        public ScriptingCryptoAirdropInputFileWindowViewModel()
        {
            Notice = "Here you will create an Input file for airdrops that have the same Crypto Amount and Memo. This can be modified after as needed.";
            Log = $"Place wallet addresses and ENS here. One per line.\r\n\r\nExample:\r\ncobmin.eth\r\n0x6458cc5902d4f9e466b599e220d1663c4718625a\r\njacobhuber.eth";
            CreateInputFileCommand = ReactiveCommand.Create(CreateInputFile);
        }

        private async void CreateInputFile()
        {
            string walletAddresses = log;
            Log = "Checking Information, please give me a moment...";
            IsEnabled = false;
            var sw = new Stopwatch();
            sw.Start();
            StringBuilder buildinvalidLines = new StringBuilder();

            if (!int.TryParse(cryptoAmount, out int number) || cryptoAmount == null)
                buildinvalidLines.Append("Error with Amount of NFTs: Please enter a number\r\n");
            if (memo?.Length > 120)
                buildinvalidLines.Append($"Error with Memo: Length greater than 120 characters.\r\n");
            if (buildinvalidLines.ToString().Contains("Error"))
            {
                Log = $"{buildinvalidLines}\r\nPlease fix the above errors in your Input file and then press Create.";
                IsEnabled = true;
                return;
            }
            string outputFilePath = $"{Constants.BaseDirectory}{Constants.OutputFolder}CryptoAirdrop_Input.txt";
            try
            {
                List<string> processedLines = new List<string>();
                List<string> stringList = new List<string>(walletAddresses.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries));


                foreach (string line in stringList)
                {
                    string processedLine = $"{cryptoAmount},{line},{memo}";
                    processedLines.Add(processedLine);
                }

                File.WriteAllLines(outputFilePath, processedLines);
                ApplicationUtilitiesUI.OpenFile(outputFilePath);
                Log = "Processing complete\r\n\r\nOutput written to: " + outputFilePath;
            }
            catch (IOException e)
            {
                Log = "An error occurred while reading/writing the file: " + e.Message;
            }

            IsEnabled = true;
        }
    }
}
