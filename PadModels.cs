using Microsoft.UI;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using WinUIShared.Enums;

namespace MediaPadderPage
{
    public class PadMainModel: INotifyPropertyChanged
    {
        private OperationState _state;
        public OperationState State
        {
            get => _state;
            set => SetProperty(ref _state, value, alsoNotify: [nameof(BeforeOperation), nameof(DuringOperation), nameof(AfterOperation)]);
        }

        private BgColourModel _selectedBgColour;
        public BgColourModel SelectedBgColour
        {
            get => _selectedBgColour;
            set => SetProperty(ref _selectedBgColour, value);
        }

        public bool BeforeOperation => State == OperationState.BeforeOperation;
        public bool DuringOperation => State == OperationState.DuringOperation;
        public bool AfterOperation => State == OperationState.AfterOperation;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] alsoNotify)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            foreach (var dep in alsoNotify) OnPropertyChanged(dep);
            return true;
        }
    }

    public class BgColourModel: INotifyPropertyChanged
    {
        private string _colourHexValue;
        public string ColourHexValue
        {
            get => _colourHexValue;
            set => SetProperty(ref _colourHexValue, value, alsoNotify: [nameof(ColourHexValueRestricted), nameof(ColourHexValueRestrictedTransparentDefault)]);
        }

        public Brush ColourHexValueRestricted => GetRestrictedHex(ColourHexValue, false);
        public Brush ColourHexValueRestrictedTransparentDefault => GetRestrictedHex(ColourHexValue, true);

        public required string ColourText { get; set; }


        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] alsoNotify)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            foreach (var dep in alsoNotify) OnPropertyChanged(dep);
            return true;
        }
        private Brush GetRestrictedHex(string? hex, bool transparentIsDefault)
        {
            if (string.IsNullOrWhiteSpace(hex)) return new SolidColorBrush(transparentIsDefault ? Colors.Transparent : Colors.Black);

            try
            {
                if(!hex.StartsWith('#')) hex = '#' + hex; //Ensure it starts with #
                hex = hex[..7]; //Take only the first 7 chars to trim any alpha values
                var brush = (Brush)XamlBindingHelper.ConvertValue(typeof(Brush), hex);
                ColourHexValue = hex; //Update the property to the restricted value (also updates the text box if user inputted a longer hex)
                return brush;
            }
            catch
            {
                ColourHexValue = string.Empty; //Reset to null if invalid hex
                return new SolidColorBrush(transparentIsDefault ? Colors.Transparent : Colors.Black);
            }
        }
    }
}
