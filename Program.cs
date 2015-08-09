namespace Reforum
{
    class Program
    {
        static void Main(string[] args)
        {

            var reforumSpb = new ReforumParser();
            reforumSpb.Builder();

            reforumSpb.xDocument.Save("reforumSpb.xml");



            //var reforumMsk = new ReforumParser();
            //reforumMsk.Builder(MSK);

            //reforumMsk.xDocument.Save("reforumMsk.xml");

        }
    }
}
