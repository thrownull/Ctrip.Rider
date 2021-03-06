﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Gms.Location;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Locations;
using Android.Media;
using Android.OS;
using Android.Support.V7.App;
using Android.Support.Design.Widget;
using Android.Support.V4.Content;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Views;
using Android.Widget;
using Ctrip.Rider.Adapters;
using Ctrip.Rider.DataModels;
using Ctrip.Rider.EventListeners;
using Ctrip.Rider.Fragments;
using Ctrip.Rider.Helpers;
using FFImageLoading;
using Google.Places;
using Java.Util;
using Refractored.Controls;
using ActionBar = Android.Support.V7.App.ActionBar;
using FragmentTransaction = Android.Support.V4.App.FragmentTransaction;
using static Android.Support.Design.Widget.NavigationView;
using Location = Android.Locations.Location;
using Toolbar = Android.Support.V7.Widget.Toolbar;
using static Android.Support.V4.View.ViewPager;
using Calendar = Java.Util.Calendar;
using Result = Android.App.Result;

namespace Ctrip.Rider
{
	[Activity(Label = "@string/app_name", Theme = "@style/CtripTheme", MainLauncher = false)]
	public class MainActivity : AppCompatActivity, IOnMapReadyCallback, IOnPageChangeListener, IOnNavigationItemSelectedListener
	{
		private readonly UserProfileEventListener _profileEventListener = new UserProfileEventListener();

		public CreateRequestEventListener RequestEventListener { get; set; }
		private FindDriverListener _findDriverListener;

		//Views
		private Toolbar _mainToolbar;
		private DrawerLayout _drawerLayout;
		private NavigationView _navView;

		//TextViews
		private TextView _accountTitleText;
		private TextView _fromLocationText;
		private TextView _toLocationText;
		private TextView _pickupText;
		private TextView _destinationText;
		private TextView _greetingsTv;
		private TextView _drawerTextUsername;
		private TextView _driverNameText;
		private TextView _tripStatusText;

		//Progresses
		private ProgressBar _pickupProgress;
		private ProgressBar _destinationProgress;

		//Layouts
		private RelativeLayout _bottomSheetRootView;
		private RelativeLayout _tripDetailsView;
		private RelativeLayout _layoutPickUp;
		private RelativeLayout _layoutDestination;
		private FrameLayout _rideInfoView;

		//Bottom-sheets
		private BottomSheetBehavior _bottomSheetRootBehavior;
		private BottomSheetBehavior _tripDetailsBehavior;
		private BottomSheetBehavior _driverAssignedBehavior;

		//Buttons
		private FloatingActionButton _myLocation;
		private Button _requestRideBtn;
		private ImageButton _callDriverBtn;

		private readonly string[] _permissionGroupLocation =
			{Manifest.Permission.AccessCoarseLocation, Manifest.Permission.AccessFineLocation};

		private const int RequestLocationId = 0;

		private GoogleMap _googleMap;
		private LocationRequest _mLocationRequest;
		private FusedLocationProviderClient _locationProviderClient;
		private Location _mLastLocation;
		private LocationCallbackHelper _mLocationCalback;

		private static readonly int UpdateInterval = 5; //5 SECONDS
		private static readonly int FastestInterval = 5;
		private static readonly int Displacement = 3; //meters
		private static readonly float Zoom = 16.0f;

		MapFunctionHelper _mapHelper;

		private LatLng _pickupLocationLatlng;
		private LatLng _destinationLatLng;
		private string _pickupAddress;
		private string _destinationAddress;
		private ViewPager _viewPager;
		private PagerAdapter _pagerAdapter;
		private List<RideTypeDataModel> _rideTypeList;

		private bool _isTripDrawn = false;
		private string _driverPhone;

		private NewTripDetails _newTripDetails;
		FindingDriverDialog _requestDriverFragment;

		private const int RequestCodePickup = 1;
		private const int RequestCodeDestination = 2;

		internal static MainActivity Instance { get; set; }

		private void ConnectControls()
		{
			_drawerLayout = FindViewById<DrawerLayout>(Resource.Id.drawerLayout);
			_mainToolbar = FindViewById<Toolbar>(Resource.Id.mainToolbar);

			SetSupportActionBar(_mainToolbar);

			SupportActionBar.Title = string.Empty;

			ActionBar actionBar = SupportActionBar;
			actionBar.SetHomeAsUpIndicator(Resource.Mipmap.ic_menu_action);
			actionBar.SetDisplayHomeAsUpEnabled(true);

			_fromLocationText = FindViewById<TextView>(Resource.Id.from_tv);
			_toLocationText = FindViewById<TextView>(Resource.Id.to_tv);
			_pickupText = FindViewById<TextView>(Resource.Id.pickupText);
			_destinationText = FindViewById<TextView>(Resource.Id.destinationText);
			_greetingsTv = FindViewById<TextView>(Resource.Id.greetings_tv);
			_layoutPickUp = FindViewById<RelativeLayout>(Resource.Id.layoutPickup);
			_layoutDestination = FindViewById<RelativeLayout>(Resource.Id.layoutDestination);

			_layoutPickUp.Click += (sender, e) => StartAutoComplete(RequestCodePickup);
			_layoutDestination.Click += (sender, e) => StartAutoComplete(RequestCodeDestination);

			_pickupProgress = FindViewById<ProgressBar>(Resource.Id.pickupProgress);
			_destinationProgress = FindViewById<ProgressBar>(Resource.Id.destiopnationProgress);

			_myLocation = FindViewById<FloatingActionButton>(Resource.Id.fab_myloc);
			_requestRideBtn = FindViewById<Button>(Resource.Id.ride_select_btn);
			_callDriverBtn = FindViewById<ImageButton>(Resource.Id.callDriverButton);

			_myLocation.Click += MyLocation_Click;
			_requestRideBtn.Click += RequestRideBtnClick;
			_callDriverBtn.Click += CallDriverBtnClick;

			_viewPager = FindViewById<ViewPager>(Resource.Id.viewPager);

			_bottomSheetRootView = FindViewById<RelativeLayout>(Resource.Id.main_sheet_root);
			_tripDetailsView = FindViewById<RelativeLayout>(Resource.Id.trip_root);
			_rideInfoView = FindViewById<FrameLayout>(Resource.Id.bottom_sheet_trip);

			_bottomSheetRootBehavior = BottomSheetBehavior.From(_bottomSheetRootView);
			_tripDetailsBehavior = BottomSheetBehavior.From(_tripDetailsView);
			_driverAssignedBehavior = BottomSheetBehavior.From(_rideInfoView);

			_bottomSheetRootBehavior.PeekHeight = BottomSheetBehavior.PeekHeightAuto;
			_bottomSheetRootBehavior.State = BottomSheetBehavior.StateHidden;

			if (!_isTripDrawn)
			{
				_tripDetailsBehavior.State = BottomSheetBehavior.StateHidden;
			}

			_greetingsTv.Text = GetGreetings();

			_navView = FindViewById<NavigationView>(Resource.Id.navView);
			_navView.ItemIconTintList = null;

			View headerView = _navView.GetHeaderView(0);
			headerView.Click += HeaderView_Click;

			_drawerTextUsername = headerView.FindViewById<TextView>(Resource.Id.accountTitle);
			_drawerTextUsername.Text = AppDataHelper.GetFullName();

			_tripStatusText = FindViewById<TextView>(Resource.Id.tripStatusText);
		    _driverNameText = FindViewById<TextView>(Resource.Id.driverNameText);

			CircleImageView accountImage = headerView.FindViewById<CircleImageView>(Resource.Id.accountImage);

			RunOnUiThread(() =>
			{
				SetProfilePic(AppDataHelper.GetFbProfilePic(), accountImage);
			});

			SetUpDrawerContent(_navView);
		}

		#region Overrides

		protected override async void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			Xamarin.Essentials.Platform.Init(this, savedInstanceState);

			Instance = this;

			SetContentView(Resource.Layout.activity_main);

			ConnectControls();

			SupportMapFragment mapFragment = (SupportMapFragment)SupportFragmentManager.FindFragmentById(Resource.Id.map);
			mapFragment.GetMapAsync(this);

			CheckLocationPermissions();
			CreateLocationRequest();

			await GetCurrentLocationAsync();
			await StartLocationUpdatesAsync();

			_profileEventListener.Create();

			_accountTitleText = FindViewById<TextView>(Resource.Id.accountTitle);
			_accountTitleText.Text = AppDataHelper.GetFullName();

			InitializePlaces();

			_rideTypeList = new List<RideTypeDataModel>();
		}

		public override bool OnOptionsItemSelected(IMenuItem item)
		{
			switch (item.ItemId)
			{
				case Android.Resource.Id.Home:
					_drawerLayout.OpenDrawer((int)GravityFlags.Left);
					return true;
				default:
					return base.OnOptionsItemSelected(item);
			}
		}

		protected override async void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

			if (requestCode == RequestCodePickup)
			{
				if (resultCode == Result.Ok)
				{
					_pickupProgress.Visibility = ViewStates.Visible;

					Place place = Autocomplete.GetPlaceFromIntent(data);
					_pickupText.Text = place.Name;
					_fromLocationText.Text = place.Name;
					_pickupLocationLatlng = place.LatLng;
					_pickupAddress = place.Name;
					_layoutPickUp.Enabled = false;

					_pickupProgress.Visibility = ViewStates.Gone;
				}
				else if (resultCode == Result.Canceled)
				{
					_pickupProgress.Visibility = ViewStates.Gone;
				}
			}

			if (requestCode == RequestCodeDestination)
			{
				if (resultCode == Result.Ok)
				{
					_destinationProgress.Visibility = ViewStates.Visible;

					Place place = Autocomplete.GetPlaceFromIntent(data);
					_destinationText.Text = place.Name;
					_toLocationText.Text = place.Name;
					_destinationLatLng = place.LatLng;
					_destinationAddress = place.Name;
					_layoutDestination.Enabled = false;

					await TripLocationsSet();
				}
				else if (resultCode == Result.Canceled)
				{
					_destinationProgress.Visibility = ViewStates.Gone;
				}
			}
		}

		public override void OnBackPressed()
		{
			if (_drawerLayout.IsDrawerOpen((int)GravityFlags.Start))
			{
				_drawerLayout.CloseDrawer((int)GravityFlags.Start);
			}
			else
			{
				if (_isTripDrawn)
				{
					ResetTrip();
				}
				else
				{
					base.OnBackPressed();
				}
			}
		}
		public void OnPageScrollStateChanged(int state)
		{ }

		public void OnPageScrolled(int position, float positionOffset, int positionOffsetPixels)
		{ }

		public void OnPageSelected(int position)
		{ }

		public bool OnNavigationItemSelected(IMenuItem menuItem)
		{
			SelectDrawerItem(menuItem.ItemId);
			return true;
		}

		private void SetUpDrawerContent(NavigationView navView)
		{
			navView.SetNavigationItemSelectedListener(this);
		}

		#endregion

		#region Click Event Handlesrs

		private void HeaderView_Click(object sender, EventArgs e)
		{
			if (!_drawerLayout.IsDrawerOpen((int) GravityFlags.Start))
			{
				return;
			}

			Android.Support.V4.App.Fragment profileFragment = new ProfileFragment();

			SupportFragmentManager
				.BeginTransaction()
				.SetCustomAnimations(Resource.Animation.slide_up_anim, Resource.Animation.slide_up_out)
				.Replace(Resource.Id.content_frame, profileFragment, profileFragment.Class.SimpleName)
				.AddToBackStack(null)
				.Commit();

			_drawerLayout.CloseDrawer((int)GravityFlags.Start);
		}

		private async void MyLocation_Click(object sender, EventArgs e)
		{
			if (_pickupLocationLatlng != null && !_isTripDrawn)
			{
				_googleMap.AnimateCamera(CameraUpdateFactory.NewLatLngZoom(_pickupLocationLatlng, Zoom));
			}
			else if (_pickupLocationLatlng == null && !_isTripDrawn)
			{
				await GetCurrentLocationAsync();
			}
		}

		private async void RequestRideBtnClick(object sender, EventArgs e)
		{
			_tripDetailsBehavior.Hideable = true;
			_bottomSheetRootBehavior.Hideable = true;
			_tripDetailsBehavior.State = BottomSheetBehavior.StateHidden;
			_bottomSheetRootBehavior.State = BottomSheetBehavior.StateHidden;

			_requestDriverFragment = FindingDriverDialog.Display(SupportFragmentManager, false, _pickupAddress, _destinationAddress, _mapHelper.durationString, _mapHelper.GetEstimatedFare());

			_newTripDetails = new NewTripDetails
			{
				DestinationAddress = _destinationAddress,
				PickupAddress = _pickupAddress,
				DestinationLat = _destinationLatLng.Latitude,
				DestinationLng = _destinationLatLng.Longitude,
				DistanceString = _mapHelper.distanceString,
				DistanceValue = _mapHelper.distance,
				DurationString = _mapHelper.durationString,
				DurationValue = _mapHelper.duration,
				EstimateFare = _mapHelper.GetEstimatedFare(),
				Paymentmethod = "cash",
				PickupLat = _pickupLocationLatlng.Latitude,
				PickupLng = _pickupLocationLatlng.Longitude,
				Timestamp = DateTime.Now
			};

			RequestEventListener = new CreateRequestEventListener(_newTripDetails);
			RequestEventListener.NoDriverAcceptedRequest += RequestListener_NoDriverAcceptedRequest;
			RequestEventListener.DriverAccepted += RequestListener_DriverAccepted;
			RequestEventListener.TripUpdates += RequestListener_TripUpdates;
			await RequestEventListener.CreateRequestAsync();

			_findDriverListener = new FindDriverListener(_pickupLocationLatlng, _newTripDetails.RideId);
			_findDriverListener.DriversFound += FindDriverListener_DriversFound;
			_findDriverListener.DriverNotFound += FindDriverListener_DriverNotFound;
			_findDriverListener.Create();
		}

		private void CallDriverBtnClick(object sender, EventArgs e)
		{
			if (!string.IsNullOrEmpty(_driverPhone))
			{
				Android.Net.Uri uri = Android.Net.Uri.Parse("tel:" + _driverPhone);
				Intent intent = new Intent(Intent.ActionDial, uri);
				StartActivity(intent);
			}
		}

		private void RequestListener_NoDriverAcceptedRequest(object sender, EventArgs e)
		{
			RunOnUiThread(() =>
			{
				if (_requestDriverFragment != null && RequestEventListener != null)
				{
					RequestEventListener.CancelRequestOnTimeout();
					RequestEventListener = null;
					_requestDriverFragment.Dismiss();
					_requestDriverFragment = null;

					Android.Support.V7.App.AlertDialog.Builder alert = new Android.Support.V7.App.AlertDialog.Builder(this);
					alert.SetTitle(Resources.GetText(Resource.String.txtMessage));
					alert.SetMessage(Resources.GetText(Resource.String.txtNoDriversAvailable));
					alert.Show();
				}
			});
		}

		private async void RequestListener_DriverAccepted(object sender, CreateRequestEventListener.DriverAcceptedEventArgs e)
		{
			if (_requestDriverFragment != null)
			{
				_requestDriverFragment.Dismiss();
				_requestDriverFragment = null;
			}

			_driverPhone = e.AcceptedDriver.Phone;
			_driverNameText.Text = e.AcceptedDriver.Fullname;
			_tripStatusText.Text = Resources.GetText(Resource.String.txtOnTrip);

			_tripDetailsBehavior.State = BottomSheetBehavior.StateHidden;
			_driverAssignedBehavior.State = BottomSheetBehavior.StateExpanded;
		}

		private async void RequestListener_TripUpdates(object sender, CreateRequestEventListener.TripUpdatesEventArgs e)
		{
			if (e.Status == "accepted")
			{
				_tripStatusText.Text = Resources.GetText(Resource.String.txtDriverComing);
				_mapHelper.UpdateDriverLocationToPickUp(_pickupLocationLatlng, e.DriverLocation);
			}
			else if (e.Status == "arrived")
			{
				string driverArrived = Resources.GetText(Resource.String.txtDriverArrived);
				_tripStatusText.Text = driverArrived;
				_mapHelper.UpdateDriverArrived(driverArrived);

				MediaPlayer player = MediaPlayer.Create(this, Resource.Raw.alert);
				player.Start();
			}
			else if (e.Status == "ontrip")
			{
				_tripStatusText.Text = Resources.GetText(Resource.String.txtOnTrip);
				_mapHelper.UpdateLocationToDestination(e.DriverLocation, _destinationLatLng);
			}
			else if (e.Status == "ended")
			{
				_pickupLocationLatlng.Longitude = e.DriverLocation.Longitude;
				_pickupLocationLatlng.Latitude = e.DriverLocation.Latitude;
				_mLastLocation.Longitude = e.DriverLocation.Longitude;
				_mLastLocation.Latitude = e.DriverLocation.Latitude;

				RequestEventListener.EndTrip();
				RequestEventListener = null;

				//_googleMap.Clear();

				ResetTrip();

				_pickupText.Text = await _mapHelper.FindCordinateAddress(_pickupLocationLatlng);

				//await GetCurrentLocationAsync();
				//MakePaymentFragment makePaymentFragment = new MakePaymentFragment(e.Fares);
				//makePaymentFragment.Cancelable = false;
				//var trans = SupportFragmentManager.BeginTransaction();
				//makePaymentFragment.Show(trans, "payment");
				//makePaymentFragment.PaymentCompleted += (i, p) =>
				//{
				//	makePaymentFragment.Dismiss();
				//};
			}
		}

		private void FindDriverListener_DriversFound(object sender, FindDriverListener.DriverFoundEventArgs e)
		{
			RequestEventListener?.NotifyDriver(e.Drivers);
		}

		private async void FindDriverListener_DriverNotFound(object sender, EventArgs e)
		{
			if (_requestDriverFragment != null && RequestEventListener != null)
			{
				await RequestEventListener.CancelRequestAsync();
				RequestEventListener = null;
				_requestDriverFragment.Dismiss();
				_requestDriverFragment = null;

				Android.Support.V7.App.AlertDialog.Builder alert = new Android.Support.V7.App.AlertDialog.Builder(this);
				alert.SetTitle(Resources.GetText(Resource.String.txtMessage));
				alert.SetMessage(Resources.GetText(Resource.String.txtNoDriversAvailable));
				alert.Show();
			}
		}

		#endregion

		#region Map And Location

		public async void OnMapReady(GoogleMap googleMap)
		{
			_googleMap = googleMap;
			_googleMap.MyLocationEnabled = true;
			_googleMap.UiSettings.MyLocationButtonEnabled = false;
			_googleMap.UiSettings.CompassEnabled = false;
			_googleMap.UiSettings.RotateGesturesEnabled = false;
			_googleMap.UiSettings.MapToolbarEnabled = false;

			InfoWindowHelper infoWindowHelper = new InfoWindowHelper(this);
			_googleMap.SetInfoWindowAdapter(infoWindowHelper);

			_pickupProgress.Visibility = ViewStates.Visible;

			await GetCurrentLocationAsync();

			_mapHelper = new MapFunctionHelper(Resources.GetString(Resource.String.mapKey), _googleMap);

			_pickupLocationLatlng = _googleMap.CameraPosition.Target;

			_pickupAddress = await _mapHelper.FindCordinateAddress(_pickupLocationLatlng);

			_pickupText.Text = _pickupAddress;
			_fromLocationText.Text = _pickupAddress;
			_pickupProgress.Visibility = ViewStates.Gone;
		}

		private async Task GetCurrentLocationAsync()
		{
			if (!CheckLocationPermissions())
			{
				return;
			}

			_mLastLocation = await _locationProviderClient.GetLastLocationAsync();

			if (_mLastLocation != null)
			{
				LatLng currentLocation = new LatLng(_mLastLocation.Latitude, _mLastLocation.Longitude);
				_googleMap.MoveCamera(CameraUpdateFactory.NewLatLngZoom(currentLocation, Zoom));
			}
		}

		private void CurrentLocationCallback(object sender, OnLocationCapturedEventArgs e)
		{
			if (!_isTripDrawn)
			{
				_mLastLocation = e.Location;
				LatLng currentPosition = new LatLng(_mLastLocation.Latitude, _mLastLocation.Longitude);
				_googleMap.AnimateCamera(CameraUpdateFactory.NewLatLngZoom(currentPosition, Zoom));
				SetTripUi();
			}
		}

		private async Task StartLocationUpdatesAsync()
		{
			if (CheckLocationPermissions())
			{
				await _locationProviderClient.RequestLocationUpdatesAsync(_mLocationRequest, _mLocationCalback, null);
			}
		}

		private void InitializePlaces()
		{
			string mapKey = Resources.GetString(Resource.String.mapKey);

			if (!PlacesApi.IsInitialized)
			{
				PlacesApi.Initialize(this, mapKey);
			}
		}

		private void CreateLocationRequest()
		{
			_mLocationRequest = new LocationRequest();
			_mLocationRequest.SetInterval(UpdateInterval);
			_mLocationRequest.SetFastestInterval(FastestInterval);
			_mLocationRequest.SetSmallestDisplacement(Displacement);
			_mLocationRequest.SetPriority(LocationRequest.PriorityHighAccuracy);

			_locationProviderClient = LocationServices.GetFusedLocationProviderClient(this);

			_mLocationCalback = new LocationCallbackHelper(); 
			_mLocationCalback.CurrentLocation += CurrentLocationCallback;
		}

		private bool CheckLocationPermissions()
		{
			bool permissionsGranted = _permissionGroupLocation.All(x => ContextCompat.CheckSelfPermission(this, x) == Permission.Granted);

			if (!permissionsGranted)
			{
				RequestPermissions(_permissionGroupLocation, RequestLocationId);
			}

			return permissionsGranted;
		}

		#endregion

		#region Trip Configurations

		private void StartAutoComplete(int requestCode)
		{
			List<Place.Field> fields = new List<Place.Field>
			{
				Place.Field.Id, Place.Field.Name, Place.Field.LatLng, Place.Field.Address
			};

			Intent intent = new Autocomplete.IntentBuilder(AutocompleteActivityMode.Overlay, fields)
				.SetCountry("UA")
				.Build(this);

			StartActivityForResult(intent, requestCode);
		}

		private async Task TripLocationsSet()
		{
			string json = await _mapHelper.GetDirectionJsonAsync(_pickupLocationLatlng, _destinationLatLng);

			if (!string.IsNullOrEmpty(json))
			{
				_isTripDrawn = true;

				RunOnUiThread(() =>
				{
					_mapHelper.DrawTripOnMap(json);

					double estimatedFare = _mapHelper.GetEstimatedFare();
					string duration = _mapHelper.GetDuration();

					_rideTypeList.Clear();
					_rideTypeList.Add(new RideTypeDataModel { Image = Resource.Drawable.taxi_lite, RideType = Resources.GetText(Resource.String.txtLite), RidePrice = $"₴ {estimatedFare}", RiderDuration = duration });
					_rideTypeList.Add(new RideTypeDataModel { Image = Resource.Drawable.taxi_standard, RideType = Resources.GetText(Resource.String.txtStandard), RidePrice = $"₴ {estimatedFare + 20}", RiderDuration = duration });
					_rideTypeList.Add(new RideTypeDataModel { Image = Resource.Drawable.taxi_comfort, RideType = Resources.GetText(Resource.String.txtComfort), RidePrice = $"₴ {estimatedFare + 40}", RiderDuration = duration });
					_rideTypeList.Add(new RideTypeDataModel { Image = Resource.Drawable.taxi_minibus, RideType = Resources.GetText(Resource.String.txtMinibus), RidePrice = $"₴ {estimatedFare + 80}", RiderDuration = duration });
					_rideTypeList.Add(new RideTypeDataModel { Image = Resource.Drawable.taxi_driver, RideType = Resources.GetText(Resource.String.txtDriver), RidePrice = $"₴ {estimatedFare * 3}", RiderDuration = duration });

					_pagerAdapter = new RidePagerAdapter(this, _rideTypeList);
					_viewPager.Adapter = _pagerAdapter;
					_viewPager.AddOnPageChangeListener(this);

					_bottomSheetRootBehavior.Hideable = true;
					_bottomSheetRootBehavior.State = BottomSheetBehavior.StateHidden;

					_tripDetailsBehavior.State = BottomSheetBehavior.StateExpanded;
					_tripDetailsBehavior.Hideable = false;

					_destinationProgress.Visibility = ViewStates.Gone;

					_googleMap.SetPadding(0, 0, 0, _tripDetailsView.Height + 10);
				});
			}
		}

		private void SetTripUi()
		{
			_bottomSheetRootBehavior.State = BottomSheetBehavior.StateExpanded;
			_bottomSheetRootBehavior.Hideable = false;
			_myLocation.Visibility = ViewStates.Visible;
		}

		public void ReverseTrip()
		{
			_bottomSheetRootBehavior.Hideable = false;
			_bottomSheetRootBehavior.State = BottomSheetBehavior.StateExpanded;
		}

		private void ResetTrip()
		{
			if (!_isTripDrawn)
			{
				return;
			}

			_layoutPickUp.Enabled = true;
			_layoutDestination.Enabled = true;
			_pickupProgress.Visibility = ViewStates.Gone;
			_destinationProgress.Visibility = ViewStates.Gone;
			_greetingsTv.Text = GetGreetings();
			_destinationText.Text = Resources.GetText(Resource.String.txtWhereToGo);

			_isTripDrawn = false;
			_googleMap.Clear();

			_tripDetailsBehavior.Hideable = true;
			_driverAssignedBehavior.Hideable = true;
			_bottomSheetRootBehavior.Hideable = false;
			_tripDetailsBehavior.State = BottomSheetBehavior.StateHidden;
			_bottomSheetRootBehavior.State = BottomSheetBehavior.StateExpanded;
			_driverAssignedBehavior.State = BottomSheetBehavior.StateHidden;

			RunOnUiThread(() =>
			{
				_googleMap.AnimateCamera(CameraUpdateFactory.NewLatLngZoom(_pickupLocationLatlng, 17.0f));
				_googleMap.SetPadding(0, 0, 0, _bottomSheetRootView.Height);
			});
		}

		#endregion

		private string GetGreetings()
		{
			string name = AppDataHelper.GetFirstname();
			string greeting = null;

			Date date = new Date();
			Calendar calendar = Calendar.Instance;
			calendar.Time = date;

			int hour = calendar.Get(CalendarField.HourOfDay);

			if (hour >= 12 && hour <= 18)
			{
				greeting = Resources.GetText(Resource.String.txtGoodAfternoon);
			}
			else if (hour > 18 && hour < 21)
			{
				greeting = Resources.GetText(Resource.String.txtGoodEvening);
			}
			else if (hour >= 21 && hour < 24)
			{
				greeting = Resources.GetText(Resource.String.txtGoodNight);
			}
			else
			{
				greeting = $"{Resources.GetText(Resource.String.txtGoodMorning)}";
			}

			return $"{greeting}, {name}";
		}

		private void SelectDrawerItem(int itemId)
		{
			Android.Support.V4.App.Fragment fragment = null;

			switch (itemId)
			{
				case Resource.Id.action_payments:
					fragment = new PaymentsFragment();
					break;
				case Resource.Id.action_history:
					fragment = new PlacesHistory();
					break;
				case Resource.Id.action_promos:
					break;
				case Resource.Id.action_support:
					break;
			}

			if (fragment != null)
			{
				SupportFragmentManager
					.BeginTransaction()
					.SetCustomAnimations(Resource.Animation.slide_up_anim, Resource.Animation.slide_up_out)
					.Replace(Resource.Id.content_frame, fragment)
					.AddToBackStack(null)
					.Commit();
			}

			_drawerLayout.CloseDrawer(GravityCompat.Start);
		}

		private async void SetProfilePic(string providerId, CircleImageView accountImage)
		{
			try
			{
				await ImageService.Instance
					.LoadUrl($"https://graph.facebook.com/{providerId}/picture?type=normal")
					.LoadingPlaceholder("boy_new", FFImageLoading.Work.ImageSource.CompiledResource)
					.Retry(3, 200)
					.IntoAsync(accountImage);
			}
			catch
			{
				// ignored
			}
		}
	}
}