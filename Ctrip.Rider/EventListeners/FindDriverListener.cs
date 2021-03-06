﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Android.Gms.Maps.Model;
using Com.Google.Maps.Android;
using Firebase.Database;
using Ctrip.Rider.DataModels;
using Ctrip.Rider.Helpers;

namespace Ctrip.Rider.EventListeners
{
    public class FindDriverListener : Java.Lang.Object, IValueEventListener
    {
	    //Events
        public class DriverFoundEventArgs : EventArgs
        {
            public List<AvailableDriver> Drivers { get; set; }
        }

        public event EventHandler<DriverFoundEventArgs> DriversFound;
        public event EventHandler DriverNotFound;

        //Ride Details
        readonly LatLng mPickupLocation;
        string _mRideId;

        //Available Drivers
        List<AvailableDriver> availableDrivers = new List<AvailableDriver>();

        public FindDriverListener(LatLng pickupLocation, string rideid)
        {
            mPickupLocation = pickupLocation;
            _mRideId = rideid;
        }

        public void OnCancelled(DatabaseError error)
        {

        }

        public void OnDataChange(DataSnapshot snapshot)
        {
            if (snapshot.Value != null)
            {
                var child = snapshot.Children.ToEnumerable<DataSnapshot>();
                availableDrivers.Clear();

                foreach (DataSnapshot data in child)
                {
                    if (data.Child("ride_id").Value != null)
                    {
                        if (data.Child("ride_id").Value.ToString() == "waiting")
                        {
                            //Get Driver Location;
                            double latitude = double.Parse(data.Child("location").Child("latitude").Value.ToString(), new CultureInfo("en-US"));
                            double longitude = double.Parse(data.Child("location").Child("longitude").Value.ToString(), new CultureInfo("en-US"));
                            LatLng driverLocation = new LatLng(latitude, longitude);
                            AvailableDriver driver = new AvailableDriver();

                            //Compute Distance Between Pickup Location and Driver Location
                            driver.DistanceFromPickup = SphericalUtil.ComputeDistanceBetween(mPickupLocation, driverLocation);
                            driver.Id = data.Key;
                            availableDrivers.Add(driver);
                        }
                    }
                }

                if (availableDrivers.Count > 0)
                {
                    availableDrivers = availableDrivers.OrderBy(o => o.DistanceFromPickup).ToList();
                    DriversFound?.Invoke(this, new DriverFoundEventArgs { Drivers = availableDrivers });
                }
                else
                {
                    DriverNotFound?.Invoke(this, new EventArgs());
                }
            }
            else
            {
                DriverNotFound?.Invoke(this, new EventArgs());
            }
        }

        public void Create()
        {
            FirebaseDatabase database = AppDataHelper.GetDatabase();
            DatabaseReference findDriverRef = database.GetReference("driversAvailable");
            findDriverRef.AddListenerForSingleValueEvent(this);
        }
    }
}
