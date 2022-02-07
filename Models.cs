using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer;



namespace VerintVideoStats
{
    public class CamContext : DbContext
    {

        public string _connString { get; set; }
        public CamContext(string connString)
        {
            this.Database.SetConnectionString(connString);
        }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlServer();
        public DbSet<CamInfo> Cams { get; set; }

    }

    [Keyless]
    public class CamInfo
    {
        public int CamId { get; set; }
        public string IPAddress { get; set; }
        public string MACAddress { get; set; }
        public string Description { get; set; }
        public string Recorder { get; set; }
        public string Device { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public int RecorderId { get; set; }

        public override string ToString()
        {
            return $"{CamId};{IPAddress};{MACAddress};{Description};{Recorder};{Manufacturer};{Model}";
        }
    }

    

}