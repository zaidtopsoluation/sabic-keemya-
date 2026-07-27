using Keemya.Frontend.Models;
using Keemya.Frontend.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Keemya.Frontend.Views
{
    public partial class CommandConfigView : UserControl
    {
        // The item currently being dragged
        private CommandConfigDto? _draggedItem;
        private Point _dragStartPoint;
        private bool _isDragging = false;

        public CommandConfigView()
        {
            InitializeComponent();
        }

        // ── Drag initiated from the grip handle ─────────────────────────────
        private void DragHandle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            if (sender is FrameworkElement handle)
            {
                var border = FindParentBorder(handle);
                if (border?.Tag is CommandConfigDto item)
                {
                    _draggedItem = item;
                    _dragStartPoint = e.GetPosition(null);
                    _isDragging = false;
                    handle.CaptureMouse();
                }
            }
        }

        private void DragHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedItem == null || e.LeftButton != MouseButtonState.Pressed || _isDragging) return;

            Point pos = e.GetPosition(null);
            Vector diff = _dragStartPoint - pos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isDragging = true;
                
                if (sender is DependencyObject depObj)
                {
                    var border = FindParentBorder(depObj);
                    if (border != null)
                    {
                        DragDrop.DoDragDrop(
                            border,
                            new DataObject("CommandConfigDto", _draggedItem),
                            DragDropEffects.Move);
                    }
                }
                
                _isDragging = false;
                _draggedItem = null;
                
                if (sender is FrameworkElement handle)
                {
                    handle.ReleaseMouseCapture();
                }
            }
        }

        private void DragHandle_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _draggedItem = null;
            _isDragging = false;
            if (sender is FrameworkElement handle)
            {
                handle.ReleaseMouseCapture();
            }
        }

        // ── DragOver on the whole list (visual feedback) ─────────────────────
        private void CommandList_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("CommandConfigDto"))
                e.Effects = DragDropEffects.None;
            else
                e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        // ── DragOver on each row — highlight drop target ─────────────────────
        private void CommandRow_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("CommandConfigDto"))
                e.Effects = DragDropEffects.None;
            else
            {
                e.Effects = DragDropEffects.Move;
                // Highlight the row being hovered
                if (sender is Border border)
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241)); // indigo
            }
            e.Handled = true;
        }

        // ── DragLeave on each row — reset highlight ──────────────────────────
        private void CommandRow_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)); // #1E293B
        }

        // ── Drop on each row — trigger reorder ──────────────────────────────
        private void CommandRow_Drop(object sender, DragEventArgs e)
        {
            // Reset highlight
            if (sender is Border border)
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)); // #1E293B

            if (!e.Data.GetDataPresent("CommandConfigDto")) return;

            var dragged = e.Data.GetData("CommandConfigDto") as CommandConfigDto;
            var target  = (sender as Border)?.Tag as CommandConfigDto;

            if (dragged == null || target == null || dragged.Id == target.Id) return;

            // Call the ViewModel's ReorderCommand
            if (DataContext is CommandConfigViewModel vm)
                vm.ReorderCommand.Execute((dragged, target));

            e.Handled = true;
        }

        // ── Drop on the list itself (fallback) ──────────────────────────────
        private void CommandList_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        // ── Helper: find the nearest parent Border ───────────────────────────
        private static Border? FindParentBorder(DependencyObject child)
        {
            DependencyObject? parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is Border b) return b;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}
