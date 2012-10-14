﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using DcMetroLib.Common;
using DcMetroLib.Data;
using DcMetroLib.Interfaces;

namespace DcMetroLib.MetroService
{
    public class MetroManager
    {
        #region Data

        private const string BaseUrl = "http://api.wmata.com/";

        private static Lazy<MetroManager> _instance = new Lazy<MetroManager>();

        public static MetroManager Instance
        {
            get { return _instance.Value; }
        }

        public  MetroManager()
        {
            
        }

        #endregion

        #region Api Key

        private String ApiKey { get; set; }

        public void RegisterApiKey(string key)
        {
            ApiKey = key;
        }

        #endregion

        #region Outbound Calls

        public Task<List<LineInfo>> GetLineInformation()
        {
            return GetList<LineInfo>("Rail.svc/Lines", "Lines");
        }

        public Task<List<RailIncidentData>> GetRailIncidents()
        {
            return GetList<RailIncidentData>("Incidents.svc/Incidents", "Incidents");
        }

        public Task<StationInfo> GetStationInfo(string stationCode)
        {
            return GetStationInfo(new StationInfo { Code = stationCode });
        }

        public Task<StationInfo> GetStationInfo(StationInfo stationCode)
        {
            return Get<StationInfo>("Rail.svc/StationInfo?StationCode=" + stationCode.Code);
        }


        public Task<List<MetroPathItem>> GetStationsBetween(String fromCode, String toCode)
        {
            return GetStationsBetween(new StationInfo {Code = fromCode}, new StationInfo {Code = toCode});
        }

        public Task<List<MetroPathItem>> GetStationsBetween(StationInfo from, StationInfo to)
        {
            return GetList<MetroPathItem>(String.Format("Rail.svc/Path?FromStationCode={0}&ToStationCode={1}", from.Code, to.Code), "Path");
        }

        public Task<List<StationInfo>> GetStationsByLine(LineCodeType lineCode)
        {
            string url;

            // get stations for a specific line
            if(lineCode != LineCodeType.All)
            {
                string lineCodeString = "?LineCode=" + lineCode.ToCode();
                url = "Rail.svc/Stations" + lineCodeString;
            }

            // list all stations
            else
            {
                url = "Rail.svc/Stations";
            }

            return GetList<StationInfo>(url, "Stations");

        }

        public Task<List<TrainArrivalTime>> GetArrivalTimesForStations(List<StationInfo> stations)
        {
            return GetList<TrainArrivalTime>("StationPrediction.svc/GetPrediction/" + stations.FoldToCommaDelimitedList(s => s.Code), "Trains");
        }

        public Task<List<StationEntrance>> GetNearestEntrances(double lat, double lon, int radiusInMeters)
        {
            return GetList<StationEntrance>(String.Format("Rail.svc/StationEntrances?lat={0}&lon={1}&radius={2}", lat, lon, radiusInMeters), "Entrances");
        }

        #endregion

        #region Helpers

        private String GetKeyedUrl(string svc)
        {
            return AppendApiKey(BaseUrl + svc);
        }

        private Task<List<T>> GetList<T>(string svc, string term) where T : class, IMetroData
        {
            var taskSource = new TaskCompletionSource<List<T>>();

            GetXmlFromUrl(GetKeyedUrl(svc)).ContinueWith(data =>
            {
                var items = MetroBuilder<T>.BuildList(data.Result, term);

                taskSource.SetResult(items);
            });

            return taskSource.Task;
        }

        private Task<T> Get<T>(string svc) where T : class, IMetroData
        {
            var taskSource = new TaskCompletionSource<T>();

            GetXmlFromUrl(GetKeyedUrl(svc)).ContinueWith(data =>
            {
                var item = MetroBuilder<T>.Build(data.Result);

                taskSource.SetResult(item);
            });

            return taskSource.Task;
        }

        private static Task<XDocument> GetXmlFromUrl(string url)
        {
            var taskCompletionSource = new TaskCompletionSource<XDocument>();

            var client = new WebClient();

            client.OpenReadCompleted += (sender, e) =>
                                            {
                                                XDocument xdoc;
                                                using (var str = e.Result)
                                                {
                                                    xdoc = XDocument.Load(str);
                                                }
                                                
                                                taskCompletionSource.SetResult(xdoc);
                                            };

            client.OpenReadAsync(new Uri(url, UriKind.Absolute));

            return taskCompletionSource.Task;
        }

        private string AppendApiKey(string s)
        {
            if(s.Contains("?"))
            {
                return GetApiKeyMultiple(s);
            }
            return GetApiKeySingle(s);
        }

        /// <summary>
        /// Use when you have no url paramters in the url string
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private string GetApiKeySingle(string s)
        {

            return s + "?" + ApiKeyUrlItem;
        }

        /// <summary>
        /// Use when you have multiple url paramters in the url string
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private string GetApiKeyMultiple(string s)
        {
            return s + "&" + ApiKeyUrlItem;
        }

        private string ApiKeyUrlItem
        {
            get { return "api_key=" + ApiKey; }
        }

        #endregion
    }

    internal static class MetroBuilder<T> where T : class, IMetroData
    {
        public static List<T> BuildList(XDocument root, string childName)
        {
            if (root != null && root.Root != null)
            {
                XNamespace df = root.Root.Name.Namespace;

                var val = (from items in root.Descendants(df + childName)
                           from item in items.Elements()
                           select (Activator.CreateInstance(typeof(T)) as T).Decode(item, df) as T).ToList();

                return val;
            }
            return null;
        }

        public static T Build(XDocument root)
        {
            if (root != null && root.Root != null)
            {
                XNamespace df = root.Root.Name.Namespace;

                return (Activator.CreateInstance(typeof (T)) as T).Decode(root.Root, df) as T;
            }
            return null;
        }
    }
}


