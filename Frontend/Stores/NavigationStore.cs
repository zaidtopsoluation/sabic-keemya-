using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Keemya.Frontend.Stores
{
    public class NavigationStore
    {
        private ObservableObject? _currentViewModel;
        public ObservableObject? CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                if (_currentViewModel is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _currentViewModel = value;
                CurrentViewModelChanged?.Invoke();
            }
        }

        public event Action? CurrentViewModelChanged;
    }
}
