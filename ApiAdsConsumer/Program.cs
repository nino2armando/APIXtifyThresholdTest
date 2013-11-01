using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http;

namespace ApiAdsConsumer
{
    class Program
    {

        // ------------------------------------TEST VARIALBES -------[CHANGE THESE]
        public static string TimeOut = "20";
        public static string location = "7399";
        public static List<string> accounts = new List<string>() { "4715101", "3283466", "5455467", "7886" };

        // ----------------------------------- [DO NOT CHANGE]
        public static string Path = string.Format(@"..\..\Results\test_{0}_secondesTimeOut.csv",TimeOut);


        static void Main(string[] args)
        {

            // display
            DisplayWarning();

            DisplayHeading(10);

            Console.ReadKey();
        }

        public static void DisplayWarning()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("DISABLE AUTHENTICATION : Pbp.MobileAppsParkingAPI.ServiceModel ----> GetFeedItem");
            Console.WriteLine("DISABLE AUTHENTICATION : Pbp.MobileAppsParkingAPI.ServiceModel ----> PostParkingSession");
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static PostParkingSession GetSession(string account)
        {
            return  new PostParkingSession
            {
                ConsentToOffers = true,
                AccountNumber = account,
                AmountCharged = 1,
                ConsentToAlerts = true,
                StartTimeRfc3339 = "2013-10-02T11:27:00.0000-0800",
                EndTimeRfc3339 = "2013-10-02T11:28:00.0000-0800",
                WavePayMemberId = int.Parse(account),
                LocationNumber = location,
                Language = "EN"
            };
        }

        public static void DisplayHeading(int numTry)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("--------------------------------------------------------------");
            Console.WriteLine("-------------------Testing at {0} sec timeOut-----------------", TimeOut);
            Console.WriteLine("--------------------------------------------------------------");
            Console.ForegroundColor = ConsoleColor.Gray;

            var csvModelList = new List<CSVModel>();
            foreach (string account in accounts)
            {
                var data = GetSession(account);
                for (int i = 0; i < numTry; i++)
                {
                    var previously = GuaranteeContent(BuildUrl(string.Format("/v2/members/{0}/feedItems?sinceTimeStampRfc3339=2013-09-29T14:44:21.0000-0700&howMany=50&format=json", account)));
                    var elapsedTime = TriggerParkingSession(data, BuildUrl(string.Format("/v2/members/{0}/parkingSessions", account)));
                    var current = GuaranteeContent(BuildUrl(string.Format("/v2/members/{0}/feedItems?sinceTimeStampRfc3339=2013-09-29T14:44:21.0000-0700&howMany=50&format=json", account)));

                    Console.WriteLine(
                        current.Total > previously.Total ? "{0}) --------- {1} ------- Response [{2}]" : "{0}) --------- {1} ------- No Response [{2}]", i,
                        elapsedTime, current.StatusCode);

                    csvModelList.Add(new CSVModel
                    {
                        NumRow = i,
                        ResponseStatus = current.Total > previously.Total ? "Response" : "No Response",
                        StatusCode = current.StatusCode,
                        ElapsedTime = elapsedTime
                    });
                }
            }

            CreateCsv(csvModelList);
        }

        public static void CreateCsv(IList<CSVModel> collection)
        {
            int length = collection.Count;

            var sb = new StringBuilder();
            sb.AppendLine("#,Miliseconds,HttpStatus,ResponseStatus");
            for (int i = 0; i < length; i++)
            {
                sb.AppendLine(string.Format("{0},{1},{2},{3}",collection[i].NumRow, collection[i].ElapsedTime, collection[i].StatusCode, collection[i].ResponseStatus));
            }

            File.WriteAllText(Path, sb.ToString());
        }

        public static DisplayModel GuaranteeContent(string url)
        {
            var total = GetFeeds(url);
            var count = total.XtifyOuterWrapperDtos.Count(a => a.Type == "Special Offer");

            return new DisplayModel
                {
                    StatusCode = total.ResponseMessage,
                    Total = count
                }; 

        }

        public static long TriggerParkingSession(PostParkingSession data, string parkingUrl)
        {
            var client = new HttpClient();

            var jsonFormatter = new JsonMediaTypeFormatter();

            HttpContent content = new ObjectContent(data.GetType() , data, jsonFormatter);

            HttpResponseMessage response = null;

            var sw = new Stopwatch();
            sw.Start();
            
            var task = client.PostAsync(parkingUrl, content).ContinueWith(
                (requestTask) =>
                {
                    try
                    {
                        // Get HTTP response from completed task. 
                        response = requestTask.Result;
                        var readTask = response.Content.ReadAsStringAsync();
                        readTask.Wait();
                        // we not going to do this check(do it at a higher level) since we are returning httpresponseMessage
                        //response.EnsureSuccessStatusCode();
                    }
                    catch (HttpRequestException ex)
                    {
                        throw;
                    }
                    catch (WebException ex)
                    {
                        throw;
                    }
                    catch (TaskCanceledException ex)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                    finally
                    {
                        client.Dispose();
                    }
                });

            task.Wait();
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        public static MonitoringModel GetFeeds(string feedUrl)
        {
            var httpClinet = new HttpClient();
            var responseModel = new MonitoringModel();

            var task = httpClinet.GetAsync(feedUrl).ContinueWith(
                 (requestTask) =>
                 {
                     try
                     {
                         var sw = new Stopwatch();
                         sw.Start();
                         // Get HTTP response from completed task. 
                         HttpResponseMessage response = requestTask.Result;
                         if (response.StatusCode != HttpStatusCode.OK)
                         {
                             Console.ForegroundColor = ConsoleColor.Red;
                             Console.WriteLine("T24 services returned " + response.StatusCode.ToString());
                         }
                         var readTask = response.Content.ReadAsStringAsync();

                         readTask.Wait();

                         // Check that response was successful or throw exception 
                         response.EnsureSuccessStatusCode();
                         sw.Stop();

                         var items = JsonConvert.DeserializeObject<List<XtifyOuterWrapperDto>>(readTask.Result);
                         if (items.Any())
                         {
                             responseModel.XtifyOuterWrapperDtos = items;
                             responseModel.ElapsedMilliseconds = sw.ElapsedMilliseconds;
                             responseModel.ResponseMessage = response.StatusCode.ToString();
                         }
                     }
                     catch (HttpRequestException ex)
                     {
                         throw;
                     }
                     catch (WebException ex)
                     {
                         throw;
                     }
                     catch (TaskCanceledException ex)
                     {
                         throw;
                     }
                     catch (Exception ex)
                     {
                         throw;
                     }
                     finally
                     {
                         httpClinet.Dispose();
                     }
                 });

            task.Wait();
            return responseModel;
        }

        public static string BuildUrl(string action)
        {
            var baseUrl = "http://localhost:63448";
            var fullUrl = baseUrl + action;
            return fullUrl;
        }
    }

    public class CSVModel
    {
        public int NumRow { get; set; }
        public long ElapsedTime { get; set; }
        public string StatusCode { get; set; }
        public string ResponseStatus { get; set; }
    }

    public class XtifyOuterWrapperDto
    {
        public string Subject { get; set; }
        public string Content { get; set; }
        public DateTime TimeStampRfc3339 { get; set; }
        public string ImageUrlPath { get; set; }
        public string MoreDetailsUrl { get; set; }
        public string Type { get; set; }
    }

    public class MonitoringModel
    {
        public List<XtifyOuterWrapperDto> XtifyOuterWrapperDtos { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public string ResponseMessage { get; set; }
    }

    public class PostParkingSession
    {
        public int WavePayMemberId { get; set; }
        public string LocationNumber { get; set; }
        public string AccountNumber { get; set; }
        public decimal AmountCharged { get; set; }
        public string Language { get; set; }
        public string StartTimeRfc3339 { get; set; }
        public string EndTimeRfc3339 { get; set; }
        public bool ConsentToOffers { get; set; }
        public bool ConsentToAlerts { get; set; }
    }

    public class DisplayModel
    {
        public int Total { get; set; }
        public string StatusCode { get; set; }
    }
}
