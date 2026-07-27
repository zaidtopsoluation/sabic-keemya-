using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Keemya.Frontend.Services;
using Keemya.Frontend.Stores;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace Keemya.Frontend.ViewModels
{
    public partial class NotificationsViewModel : ObservableObject
    {
        private readonly NavigationStore _navigationStore;
        private readonly NotificationService _notificationService;

        [ObservableProperty]
        private ObservableCollection<NotificationItem> notifications = new();

        [ObservableProperty]
        private int unreadCount;

        private readonly System.Timers.Timer? _refreshTimer;

        public NotificationsViewModel(NavigationStore navigationStore)
        {
            _navigationStore = navigationStore;
            _notificationService = new NotificationService();

            _ = LoadNotificationsAsync();

            // Refresh list every 2 seconds while the user is viewing it
            _refreshTimer = new System.Timers.Timer(2000);
            _refreshTimer.Elapsed += async (s, e) =>
            {
                if (_navigationStore.CurrentViewModel != this)
                {
                    _refreshTimer.Stop();
                    _refreshTimer.Dispose();
                    return;
                }
                await LoadNotificationsAsync();
            };
            _refreshTimer.Start();
        }

        [RelayCommand]
        public async Task LoadNotificationsAsync()
        {
            try
            {
                var list = await _notificationService.GetNotificationsAsync();
                
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    // 1. Remove items that no longer exist in the new list
                    var newListIds = new System.Collections.Generic.HashSet<int>();
                    foreach (var item in list)
                    {
                        newListIds.Add(item.Id);
                    }

                    for (int i = Notifications.Count - 1; i >= 0; i--)
                    {
                        if (!newListIds.Contains(Notifications[i].Id))
                        {
                            Notifications.RemoveAt(i);
                        }
                    }

                    // 2. Insert new items or update/replace existing items to match the list order
                    for (int i = 0; i < list.Count; i++)
                    {
                        var newItem = list[i];
                        
                        if (i >= Notifications.Count)
                        {
                            // Append new item at the end
                            Notifications.Add(newItem);
                        }
                        else if (Notifications[i].Id == newItem.Id)
                        {
                            // Check if properties changed. If so, replace the item in place
                            if (Notifications[i].IsRead != newItem.IsRead ||
                                Notifications[i].Title != newItem.Title ||
                                Notifications[i].Message != newItem.Message)
                            {
                                Notifications[i] = newItem;
                            }
                        }
                        else
                        {
                            // The IDs don't match at index i.
                            // Find if the item exists later in the current collection
                            int existingIndex = -1;
                            for (int j = i + 1; j < Notifications.Count; j++)
                            {
                                if (Notifications[j].Id == newItem.Id)
                                {
                                    existingIndex = j;
                                    break;
                                }
                            }

                            if (existingIndex != -1)
                            {
                                // The item is present later. We should remove the obsolete items before it.
                                while (Notifications.Count > i && Notifications[i].Id != newItem.Id)
                                {
                                    Notifications.RemoveAt(i);
                                }
                                
                                // Now they match at index i. Check for updates.
                                if (Notifications[i].IsRead != newItem.IsRead ||
                                    Notifications[i].Title != newItem.Title ||
                                    Notifications[i].Message != newItem.Message)
                                {
                                    Notifications[i] = newItem;
                                }
                            }
                            else
                            {
                                // Item does not exist in the collection at all. Insert it here.
                                Notifications.Insert(i, newItem);
                            }
                        }
                    }

                    // 3. Update UnreadCount
                    int unread = 0;
                    foreach (var item in Notifications)
                    {
                        if (!item.IsRead)
                        {
                            unread++;
                        }
                    }
                    UnreadCount = unread;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading notifications: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task MarkAllAsRead()
        {
            await _notificationService.MarkAllAsReadAsync();
            await LoadNotificationsAsync();
        }

        [RelayCommand]
        private async Task ClearAll()
        {
            var result = MessageBox.Show("Are you sure you want to clear all notifications?", "Clear All", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                await _notificationService.ClearAllAsync();
                await LoadNotificationsAsync();
            }
        }

        [RelayCommand]
        private async Task DeleteNotification(NotificationItem item)
        {
            if (item != null)
            {
                await _notificationService.DeleteNotificationAsync(item.Id);
                await LoadNotificationsAsync();
            }
        }

        [RelayCommand]
        private void Back()
        {
            _navigationStore.CurrentViewModel = new DashboardViewModel(_navigationStore);
        }
    }
}
