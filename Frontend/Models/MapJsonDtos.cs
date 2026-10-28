namespace Keemya.Frontend.Models
{
    public class SirenMapItemDto
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public double lat { get; set; }
        public double lng { get; set; }
        public string? status { get; set; }
        public string? groupName { get; set; }
        public string? subtext { get; set; }
        public string? ipAddress { get; set; }
        public double batteryVoltage { get; set; }
        public bool hasACPower { get; set; }
    }

    public class SirenMapCommandDto
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? type { get; set; }
        public string? color { get; set; }
        public string? textColor { get; set; }
        public int duration { get; set; }
    }

    public class SirenHealthDataDto
    {
        public double dcVoltage { get; set; }
        public double acVoltage { get; set; }
        public double temperature { get; set; }
        public bool sirenOn { get; set; }
        public bool rotorActive { get; set; }
        public bool acOn { get; set; }
        public bool storedAc { get; set; }
        public bool dynamicAc { get; set; }
        public bool fullAlert { get; set; }
        public bool partialAlert { get; set; }
        public bool intrusion { get; set; }
        public bool strobeActive { get; set; }
        public bool biasDetected { get; set; }
        public bool systemArmed { get; set; }
        public bool systemPowerUp { get; set; }
        public bool supervisorMode { get; set; }
    }
}
