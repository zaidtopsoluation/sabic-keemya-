using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Keemya.Frontend.ViewModels;

namespace Keemya.Frontend.Views
{
    public partial class ButtonConfigView : UserControl
    {
        public ButtonConfigView()
        {
            InitializeComponent();
        }

        private void LayoutThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (DataContext is ButtonConfigViewModel vm && (sender as Thumb)?.DataContext is LayoutItem item) vm.BeginDrag(item);
        }

        private void LayoutThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (DataContext is ButtonConfigViewModel vm && (sender as Thumb)?.DataContext is LayoutItem item) vm.Drag(item, e.HorizontalChange, e.VerticalChange);
        }
    }
}
