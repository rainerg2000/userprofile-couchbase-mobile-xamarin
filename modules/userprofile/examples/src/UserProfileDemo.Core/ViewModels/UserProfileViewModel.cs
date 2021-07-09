﻿using System;
using System.Threading.Tasks;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;

using UserProfileDemo.Core.Respositories;
using UserProfileDemo.Core.Services;
using UserProfileDemo.Models;

namespace UserProfileDemo.Core.ViewModels
{
    public class UserProfileViewModel : BaseViewModel
    {
        Action LogoutSuccessful { get; set; }

        IUserProfileRepository UserProfileRepository { get; set; }
        IAlertService AlertService { get; set; }
        IMediaService MediaService { get; set; }

        // tag::userProfileDocId[]
        string UserProfileDocId => $"user::{AppInstance.User.Username}";
        // end::userProfileDocId[]

        string _name;
        public string Name
        {
            get => _name;
            set => SetPropertyChanged(ref _name, value);
        }

        string _email;
        public string Email
        {
            get => _email;
            set => SetPropertyChanged(ref _email, value);
        }

        string _address;
        public string Address
        {
            get => _address;
            set => SetPropertyChanged(ref _address, value);
        }

        byte[] _imageData;
        public byte[] ImageData
        {
            get => _imageData;
            set => SetPropertyChanged(ref _imageData, value);
        }

        string _syncStatus;
        public string SyncStatus
        {
            get => _syncStatus;
            set => SetPropertyChanged(ref _syncStatus, value);
        }

        string _syncStatusContinuous;
        public string SyncStatusContinuous
        {
            get => _syncStatusContinuous;
            set => SetPropertyChanged(ref _syncStatusContinuous, value);
        }

        private bool _isPeriodicallyReplicating;
        public bool IsPeriodicallyReplicating
        {
            get =>_isPeriodicallyReplicating; 
            set => SetPropertyChanged(ref _isPeriodicallyReplicating, value);
        }

        private bool _isContinuouslyReplicating;
        public bool IsContinuouslyReplicating
        {
            get => _isContinuouslyReplicating;
            set => SetPropertyChanged(ref _isContinuouslyReplicating, value);
        }

        ICommand _saveCommand;
        public ICommand SaveCommand
        {
            get
            {
                if (_saveCommand == null)
                {
                    _saveCommand = new Command(async() => await Save());
                }

                return _saveCommand;
            }
        }

        ICommand _selectImageCommand;
        public ICommand SelectImageCommand
        {
            get
            {
                if (_selectImageCommand == null)
                {
                    _selectImageCommand = new Command(async () => await SelectImage());
                }

                return _selectImageCommand;
            }
        }

        ICommand _logoutCommand;
        public ICommand LogoutCommand
        {
          get
          {
            if (_logoutCommand == null)
            {
              _logoutCommand = new Command(Logout);
            }

            return _logoutCommand;
          }
        }

        ICommand _syncCommand;
        public ICommand SyncCommand
        {
          get
          {
            if (_syncCommand == null)
            {
              _syncCommand = new Command(Sync);
            }

            return _syncCommand;
          }
        }

        public UserProfileViewModel(IUserProfileRepository userProfileRepository, IAlertService alertService, 
                                    IMediaService mediaService, Action logoutSuccessful)
        {
            UserProfileRepository = userProfileRepository;
            AlertService = alertService;
            MediaService = mediaService;
            LogoutSuccessful = logoutSuccessful;
            LoadUserProfile();

            UserProfileRepository.SubscribeSyncStatus()
                .Subscribe(status => SyncStatus = $"{status.Activity} - {status.Progress.Completed}/{status.Progress.Total}");
            UserProfileRepository.SubscribeSyncStatusContinuous()
                .Subscribe(status => SyncStatusContinuous = $"{status.Activity} - {status.Progress.Completed}/{status.Progress.Total}");

            this.ObservableForProperty(x=> x.IsContinuouslyReplicating)
                .Subscribe(x => SyncContinuous(x.Value));

            this.WhenAnyValue(x => x.IsPeriodicallyReplicating)
                .Select(enabled =>
                    enabled
                        ? Observable.Interval(TimeSpan.FromMilliseconds(500)).Select(elapsed => true)
                        : Observable.Return(false))
                .Switch()
                .SelectMany(async enabled =>
                {
                    if (enabled)
                    {
                        try
                        {
                            Sync();
                        }
                        catch (Exception e)
                        {
                            SyncStatus = e.ToString();
                        }
                    }

                    return Unit.Default;
                })
                .Subscribe();
        }

        async void LoadUserProfile()
        {
            IsBusy = true;

            var userProfile = await Task.Run(() =>
            {
                // tag::getUserProfileUsingRepo[]
                var up = UserProfileRepository?.Get(UserProfileDocId);
                // end::getUserProfileUsingRepo[]

                if (up == null)
                {
                    up = new UserProfile
                    {
                        Id = UserProfileDocId,
                        Email = AppInstance.User.Username
                    };
                }

                return up;
            });

            if (userProfile != null)
            {
                Name = userProfile.Name;
                Email = userProfile.Email;
                Address = userProfile.Address;
                ImageData = userProfile.ImageData;
            }

            IsBusy = false;
        }

        Task Save()
        {
            var userProfile = new UserProfile
            {
                Id = UserProfileDocId,
                Name = Name,
                Email = Email,
                Address = Address, 
                ImageData = ImageData
            };

            // tag::saveUserProfileUsingRepo[]
            bool? success = UserProfileRepository?.Save(userProfile);
            // end::saveUserProfileUsingRepo[]

            if (success.HasValue && success.Value)
            {
                return AlertService.ShowMessage(null, "Successfully updated profile!", "OK");
            }

            return AlertService.ShowMessage(null, "Error updating profile!", "OK");
        }

        async Task SelectImage()
        {
            var imageData = await MediaService.PickPhotoAsync();

            if (imageData != null)
            {
                ImageData = imageData;
            }
        }

        void Logout()
        {
          UserProfileRepository.Dispose();

          AppInstance.User = null;

          LogoutSuccessful?.Invoke();
          LogoutSuccessful = null;
        }

        void Sync()
        { 
            UserProfileRepository.Sync();
        }
        void SyncContinuous(bool start)
        {
            UserProfileRepository.SyncContinuous(start);
        }
    }
}
