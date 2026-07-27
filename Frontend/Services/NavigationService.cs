using CommunityToolkit.Mvvm.ComponentModel;
using Keemya.Frontend.Stores;
using System;

namespace Keemya.Frontend.Services
{
    public class NavigationService<TViewModel> where TViewModel : ObservableObject
    {
        private readonly NavigationStore _navigationStore;
        private readonly Func<TViewModel> _createViewModel;

        public NavigationService(NavigationStore navigationStore, Func<TViewModel> createViewModel)
        {
            _navigationStore = navigationStore;
            _createViewModel = createViewModel;
        }

        public void Navigate()
        {
            _navigationStore.CurrentViewModel = _createViewModel();
        }
    }
}
