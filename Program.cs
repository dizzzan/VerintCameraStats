
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

                         var cams = context.Cams.FromSqlRaw(@"	SELECT DISTINCT
                                    cam.id [CamId]
                                    , DEV.IPAddress [IPAddress]  
                                    , substring(convert(varchar(100), dev.DeviceGUID), 25,12) [MACAddress]  
                                    , cam.CamDesc [Description] 
                                    , COMP.compname [Recorder]
                                    , fs.Device [Device] 
                                    , r.id [RecorderId] 

                                    FROM CameraGroups CG WITH (NOLOCK) 
                                    inner join Cameras3 CAM WITH (NOLOCK) ON CG.CameraID = CAM.ID
                                    inner join CameraStreams CS WITH (NOLOCK) ON CAM.ID = CS.CameraID AND CS.StreamType = 0
                                    inner join DeviceInterfaceVideoInputEncoders DVIE WITH (NOLOCK) ON CS.VideoEncoderID = DVIE.ID
                                    inner join CameraServiceProfiles CSP WITH (NOLOCK) ON CAM.ID = CSP.CameraID
                                    inner join Devices3 DEV WITH (NOLOCK) ON DEV.ID = CSP.DeviceID
                                    inner join [devicemodelinfos] DMI WITH (NOLOCK) on DMI.deviceid=DEV.id
                                    inner join [devicemodels] DM WITH (NOLOCK) on DM.ID=DMI.DeviceModelID
                                    inner join [DeviceManufacturers] DMAN WITH (NOLOCK) on DMAN.ID=DM.ManufacturerID
                                    inner join [DeviceInterfaceVideoInputs] DIV WITH (NOLOCK) on CSP.devicevideoinputid =DIV.ID
                                    inner join computers COMP WITH (NOLOCK) on DEV.ComputerID=COMP.compid
                                    INNER JOIN recorders r WITH (NOLOCK) on comp.compid=r.id
                                    INNER JOIN RecorderGroups rg WITH (NOLOCK) on rg.Recorder1ID=r.ID or rg.Recorder2ID=r.id
                                    INNER JOIN Recorder_Location rl WITH (NOLOCK) on rl.RecorderID=r.id
                                    INNER JOIN Locations l WITH (NOLOCK)  on l.id=rl.LocationID
                                    inner join RecorderFileStorage rfs WITH (NOLOCK) on rfs.RecorderId=r.ID
                                    inner join FileStorage fs WITH (NOLOCK) on rfs.FileStorageId=fs.Id

                                    where comp.compname = {0}

                                    and IPAddress is not null
                                    
                                    order by cam.id
                                    ", rec).ToList().GroupBy(x => x.CamId);


                         Console.WriteLine($"Enumerating cameras assigned to {rec} ...");
                         Console.WriteLine();

                         foreach (var camGroup in cams)

                         {
                             var camId = camGroup.First().CamId;
                             var camName = camGroup.First().Description;
                             long aggrFolderSize = 0;

                             Console.WriteLine($"CAM {camId} - {camName} - {camGroup.Count()} device(s)");
                             foreach (var cam in camGroup)
                             {

                                 var device = cam.Device;
                                 var path = Path.Combine(device, opts.Path, $"CAM{camId.ToString("00000")}");
                                 Console.WriteLine($"   Enumerate device: {path} ...");

                                 var folderSize = GetDirectorySize(new DirectoryInfo(path), cutoff);

                                 aggrFolderSize += folderSize;

                                 Console.WriteLine($"        Folder Size: {Math.Round(folderSize / 1e9, 3)} GB");
                                 Console.WriteLine($"        Avg Bitrate: {Math.Round((decimal)folderSize / opts.Days / 86400 / 125, 3)} Kbps");

                             }

                             Console.WriteLine($"   Aggregate Size: {Math.Round(aggrFolderSize / 1e9, 3)} GB");
                             Console.WriteLine();

                             writer.WriteLine(new String[] { camId.ToString(), camName, aggrFolderSize.ToString(), opts.Days.ToString() });

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
                var str = string.Join(',', arr);
                Writer.WriteLine(str);
            }
        }


    }
}
