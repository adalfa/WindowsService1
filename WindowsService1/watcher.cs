using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace WindowsService1
{
    public partial class watcher : ServiceBase
    {
        FileSystemWatcher watch;
        BlockingCollection<string> bc;
        private readonly object _lock = new object();
        public watcher()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {


            //Timer timer = new Timer();
            //timer.Interval = 5000; // 60 seconds
            //timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            //timer.Start();
            PrepareWatcher();

            Task.Factory.StartNew(() => StartProcessing(2));
        }
        void StartProcessing(int taskCount)
        {
           
            bc = new BlockingCollection<string>();
            watch.EnableRaisingEvents = true;

            var options = new ParallelOptions { MaxDegreeOfParallelism = taskCount };
            var partitioner = Partitioner.Create(bc.GetConsumingEnumerable(), EnumerablePartitionerOptions.NoBuffering);
            var results = Parallel.ForEach(partitioner, options, work => ProcessXml(work));

         
        }
        void ProcessXml(string path)
        {
            //Do your processing here...
            //Note many events will be called multiple times, see:
            //http://weblogs.asp.net/ashben/archive/2003/10/14/31773.aspx

            Console.WriteLine(System.Threading.Thread.CurrentThread.ManagedThreadId + ":" + path);
            string fname = System.IO.Path.GetFileName(path);
            while (!IsFileIsReady(path)) System.Threading.Thread.Sleep(50);
            System.IO.File.Move(path, @"C:\bc\processed\" + fname);
        }
        private bool IsFileIsReady(string path)
        {
            //One exception per file rather than several like in the polling pattern
            try
            {
                //If we can't open the file, it's still copying
                using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return true;
                }
            }
            catch (IOException)
            {
                return false;
            }
        }

        void StopProcessing()
        {
            watch.EnableRaisingEvents = false;

            lock (_lock) //The above line doesn't guarantee no more events will be called,
                         //And Add() and CompleteAdding() can't be called concurrently
                bc.CompleteAdding();

           
            bc.Dispose();
       
        }
        private void OnTimer(object sender, ElapsedEventArgs e)
        {
            
        }

        protected override void OnStop()
        {
        }
        void PrepareWatcher()
        {
            watch = new FileSystemWatcher(@"C:\bc\work");
            watch.InternalBufferSize = 16384;
            watch.Created += (s, e) =>
            {
                lock (_lock) //Prevents race condition when stopping
                {
                    if (!bc.IsAddingCompleted)
                        bc.Add(e.FullPath);
                }
            };
        }
    }
}
