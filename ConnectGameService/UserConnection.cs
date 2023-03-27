namespace ConnectGameService
{
    public class UserConnection
    {
        public string User { get; set; }
        public string Room { get; set; }
        public bool? Turn { get; set; }
        public string? Color { get; set; }
    }
}
