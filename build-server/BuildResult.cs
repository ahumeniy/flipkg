namespace build_server
{
    public class BuildResult
    {
        public byte[] FileData { get; set; }

        public string FileName { get; set; }

        public string Errors { get; set; }

        public bool HasFiles { get; set; }
    }
}