
using System;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using CommandLine;


class VerintVideoStatus
{
    static void Main(string[] args)
    {

        Console.WriteLine("*******************************************************************");
        Parser.Default.ParseArguments<CommandLineOptions>(args)
         .WithParsed<CommandLineOptions>(opts =>
             {

                 var rec = opts.Machine ?? Dns.GetHostName();
                 var cutoff = DateTime.Now.AddDays(-opts.Days);
                 var writer = new OutputWriter(opts.OutputFile);

                 Console.WriteLine($"Using cutoff {cutoff}");

                 Console.WriteLine("*******************************************************************");

                 using (SqlConnection connection = new SqlConnection(opts.ConnString))
                 {
                     connection.Open();

                     String sql = @"	SELECT DISTINCT
                                    cam.id [CamId] -- 0
                                    , DEV.IPAddress [IP Address]  -- 1
                                    , substring(convert(varchar(100), dev.DeviceGUID), 25,12) [MAC Address]  -- 2
                                    , cam.CamDesc [Camera Name] -- 3
                                    , COMP.compname [Recorder] -- 4
                                    , fs.Device [Device] -- 5
                                    , r.id [RecorderID] -- 6

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
                                    INNER JOIN recorders r on comp.compid=r.id
                                    INNER JOIN RecorderGroups rg on rg.Recorder1ID=r.ID or rg.Recorder2ID=r.id
                                    INNER JOIN Recorder_Location rl on rl.RecorderID=r.id
                                    INNER JOIN Locations l on l.id=rl.LocationID
                                    inner join RecorderFileStorage rfs on rfs.RecorderId=r.ID
                                    inner join FileStorage fs on rfs.FileStorageId=fs.Id

                                    where comp.compname = @host

                                    and IPAddress is not null";

                     using (SqlCommand command = new SqlCommand(sql, connection))
                     {

                         command.Parameters.Add(new SqlParameter("@host", System.Data.SqlDbType.VarChar));
                         command.Parameters["@host"].Value = rec;

                         using (SqlDataReader reader = command.ExecuteReader())
                         {

                             Console.WriteLine($"Enumerating cameras assigned to {rec} ...");
                             Console.WriteLine();


                             while (reader.Read())
                             {
                                 var camId = reader.GetInt32(0);
                                 var camName = reader.GetString(3);
                                 var device = reader.GetString(5);
                                 var path = Path.Combine(device, opts.Path, $"CAM{camId.ToString("00000")}");

                                 Console.WriteLine($"CAM {camId} - {camName} - {path}");

                                 var folderSize = GetDirectorySize(new DirectoryInfo(path), cutoff);
                                 Console.WriteLine($"        Total Size: {Math.Round(folderSize / 1e9, 3)} GB");
                                 Console.WriteLine($"        Avg Bitrate: {Math.Round((decimal)folderSize / opts.Days / 86400 / 125, 3)} Kbps");
                                 Console.WriteLine();

                                 writer.WriteLine(new String[] {camId.ToString(), camName, device, path, folderSize.ToString(), opts.Days.ToString() });
                             }
                         }
                     }
                 }
             });

    }



    static long GetDirectorySize(DirectoryInfo directoryInfo, DateTime cutoff)
    {
        var startDirectorySize = default(long);
        if (directoryInfo == null || !directoryInfo.Exists)
            return startDirectorySize; //Return 0 while Directory does not exist.

        //Add size of files in the Current Directory to main size.
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

            Console.WriteLine($"Writing output to {path}");
        }

    }

    public void WriteLine(string text)
    {
        if (Writer != null) 
            Writer.WriteLine(text);
    }

    public void WriteLine(string[] arr) {
        if (Writer != null) 
            Writer.WriteLine(string.Join(',', arr));
    }

    
}