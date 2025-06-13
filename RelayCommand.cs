using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;

public class RelayCommand : ICommand
{
    private readonly Func<object, Task> _execute;
    private readonly Predicate<object> _canExecute;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    // Construtor unificado para métodos síncronos e assíncronos
    public RelayCommand(Func<object, Task> execute, Predicate<object> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    // Sobrecarga para facilitar o uso com métodos síncronos sem parâmetros
    public RelayCommand(Action execute, Func<bool> canExecute = null)
        : this(p => Task.Run(execute), p => canExecute?.Invoke() ?? true)
    {
    }

    // Sobrecarga para facilitar o uso com métodos síncronos com parâmetros
    public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        : this(p => Task.Run(() => execute(p)), canExecute)
    {
    }

    // Sobrecarga para facilitar o uso com métodos assíncronos sem parâmetros
    public RelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        : this(p => execute(), p => canExecute?.Invoke() ?? true)
    {
    }

    public bool CanExecute(object parameter)
    {
        return !_isExecuting && (_canExecute == null || _canExecute(parameter));
    }

    public async void Execute(object parameter)
    {
        if (CanExecute(parameter))
        {
            try
            {
                _isExecuting = true;
                RaiseCanExecuteChanged(); // Desabilita o botão
                await _execute(parameter);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged(); // Habilita o botão novamente
            }
        }
    }

    // Este é o método que estava faltando!
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}
