
using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.EntityFrameworkCore;

namespace VerintVideoStats
{
    class VerintVideoStats
    {
        static void Main(string[] args)
        {

             Parser.Default.ParseArguments<CommandLineOptions>(args)
             .WithParsed<CommandLineOptions>(opts =>
                 {

                     var rec = opts.Machine ?? Dns.GetHostName();
                     var cutoff = DateTime.Now.AddDays(-opts.Days);
                     var writer = new OutputWriter(opts.OutputFile);

                     Console.WriteLine($"Using cutoff {cutoff}");

                     Console.WriteLine("\n*******************************************************************\n");

                     using (var context = new CamContext(opts.ConnString))
                     {

                        var sql = File.Exists("query.sql") ? File.ReadAllText("query.sql") : @"	SELECT DISTINCT
                                    cam.id [CamId]
                                    , DEV.IPAddress [IPAddress]  
                                    , substring(convert(varchar(100), dev.DeviceGUID), 25,12) [MACAddress]  
                                    , cam.CamDesc [Description] 
                                    , dman.ManufacturerName [Manufacturer]
                                    , dm.ModelName [Model]
                                    , COMP.compname [Recorder]
                                    , fs.Device [Device] 
                                    , r.id [RecorderId] 

                                    FROM CameraGroups CG WITH (NOLOCK) 
                                    LEFT JOIN Cameras3 CAM WITH (NOLOCK) ON CG.CameraID = CAM.ID
                                    LEFT JOIN CameraStreams CS WITH (NOLOCK) ON CAM.ID = CS.CameraID AND CS.StreamType = 0
                                    LEFT JOIN DeviceInterfaceVideoInputEncoders DVIE WITH (NOLOCK) ON CS.VideoEncoderID = DVIE.ID
                                    LEFT JOIN CameraServiceProfiles CSP WITH (NOLOCK) ON CAM.ID = CSP.CameraID
                                    LEFT JOIN Devices3 DEV WITH (NOLOCK) ON DEV.ID = CSP.DeviceID
                                    LEFT JOIN [devicemodelinfos] DMI WITH (NOLOCK) on DMI.deviceid=DEV.id
                                    LEFT JOIN [devicemodels] DM WITH (NOLOCK) on DM.ID=DMI.DeviceModelID
                                    LEFT JOIN [DeviceManufacturers] DMAN WITH (NOLOCK) on DMAN.ID=DM.ManufacturerID
                                    LEFT JOIN [DeviceInterfaceVideoInputs] DIV WITH (NOLOCK) on CSP.devicevideoinputid =DIV.ID
                                    LEFT JOIN computers COMP WITH (NOLOCK) on DEV.ComputerID=COMP.compid
                                    LEFT JOIN recorders r WITH (NOLOCK) on comp.compid=r.id
                                    LEFT JOIN RecorderGroups rg WITH (NOLOCK) on rg.Recorder1ID=r.ID or rg.Recorder2ID=r.id
                                    LEFT JOIN Recorder_Location rl WITH (NOLOCK) on rl.RecorderID=r.id
                                    LEFT JOIN Locations l WITH (NOLOCK)  on l.id=rl.LocationID
                                    LEFT JOIN RecorderFileStorage rfs WITH (NOLOCK) on rfs.RecorderId=r.ID
                                    LEFT JOIN FileStorage fs WITH (NOLOCK) on rfs.FileStorageId=fs.Id

                                    where comp.compname = {0}

                                    and IPAddress is not null
                                    
                                    order by cam.id
                                    ";

                         var cams = context.Cams.FromSqlRaw(sql, rec).ToList().GroupBy(x => x.CamId);


                         Console.WriteLine($"Enumerating cameras assigned to {rec} ...");
                         Console.WriteLine();

                         foreach (var camGroup in cams)

                         {
                             var camId = camGroup.First().CamId;
                             var camName = camGroup.First().Description;
                             long aggrFolderSize = 0;
                             string camInfo = "";

                             Console.WriteLine($"CAM {camId} - {camName} - {camGroup.Count()} device(s)");
                             foreach (var cam in camGroup)
                             {
                                 camInfo = cam.ToString();
                                 var device = cam.Device;
                                 var path = Path.Combine(device.Length == 1 ? $"{device}:" : device, opts.Path, $"CAM{camId.ToString("00000")}");
                                 Console.WriteLine($"   Enumerate device: {path} ...");

                                 var folderSize = GetDirectorySize(new DirectoryInfo(path), cutoff);

                                 aggrFolderSize += folderSize;

                                 Console.WriteLine($"        Folder Size: {Math.Round(folderSize / 1e9, 3)} GB");
                                 Console.WriteLine($"        Avg Bitrate: {Math.Round((decimal)folderSize / opts.Days / 86400 / 125, 3)} Kbps");

                             }

                             Console.WriteLine($"   Aggregate Size: {Math.Round(aggrFolderSize / 1e9, 3)} GB");
                             Console.WriteLine();

                             writer.WriteLine(new String[] { camInfo, aggrFolderSize.ToString(), opts.Days.ToString() });

                         }

                     }
                 });
        }



        static long GetDirectorySize(DirectoryInfo directoryInfo, DateTime cutoff)
        {
            var startDirectorySize = default(long);
            if (directoryInfo == null || !directoryInfo.Exists)
                return startDirectorySize;

            foreach (var fileInfo in directoryInfo.GetFiles())
            {
                if (fileInfo.LastWriteTime >= cutoff)
                {
                    System.Threading.Interlocked.Add(ref startDirectorySize, fileInfo.Length);
                }
            }

            System.Threading.Tasks.Parallel.ForEach(directoryInfo.GetDirectories(), (subDirectory) =>
                System.Threading.Interlocked.Add(ref startDirectorySize, GetDirectorySize(subDirectory, cutoff)));

            return startDirectorySize;
        }

    }

    class OutputWriter
    {

        private StreamWriter Writer { get; set; }
        public OutputWriter(string path)
        {

            if (!string.IsNullOrEmpty(path))
            {

                if (File.Exists(path))
                {
                    throw new Exception("Output file aleady exists.");
                }
                Writer = File.CreateText(path);
                Writer.AutoFlush = true;

                Console.WriteLine($"Writing output to {path}");
            }

        }

        public void WriteLine(string text)
        {
            if (Writer != null)
                Writer.WriteLine(text);
        }

        public void WriteLine(string[] arr)
        {
            if (Writer != null)
            {
                var str = string.Join(';', arr);
                Writer.WriteLine(str);
            }
        }


    }
}
