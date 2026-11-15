namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Windows.Input;

    /// <summary>The base of the view models: it tells the view which of its properties changed.</summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>Gives a field its new value; true, after telling the view, when it changed.</summary>
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    /// <summary>A command of a view model: what a button of the view does, with the parameter the view gives it.</summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> execute;

        public RelayCommand(Action execute)
            : this(_ => execute())
        {
        }

        public RelayCommand(Action<object?> execute) => this.execute = execute;

        /// <inheritdoc/>
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        /// <inheritdoc/>
        public bool CanExecute(object? parameter) => true;

        /// <inheritdoc/>
        public void Execute(object? parameter) => execute(parameter);
    }
}
