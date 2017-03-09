using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using Impinj.OctaneSdk;
using System;
using System.Data;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.RegularExpressions;

using GradDisplayMat.Models;

namespace GradDisplayMat
{
    class Program
    {
        static ImpinjReader reader = new ImpinjReader();
        static SqlConnection conn = null;

        static void Main(string[] args)
        {
            // Defines the sources of configuration information for the 
            // application.
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            // Create the configuration object that the application will
            // use to retrieve configuration information.
            var configuration = builder.Build();

            // Retrieve the configuration information.
            var configValue = configuration.GetConnectionString("MainDisplayDB");


            TelepromptDbContext.ConnectionString = configValue;
            var _contextTeleprompt = new TelepromptDbContext();

            GraduateDbContext.ConnectionString = configValue;
            var _contextGraduate = new GraduateDbContext();

            QueueDbContext.ConnectionString = configValue;
            var _contextQueue = new QueueDbContext();


            Console.WriteLine("a. The read tag will automatically be served to the Teleprompt Screen, if the screen is empty");
            Console.WriteLine("b. If the screen is not empty, await the read tag to queue until available");
            Console.WriteLine("c. If the screen is empty and there's queue waiting. Pop the queue and load to Teleprompt screen");
            Console.Write("\n\n\nReadding Tag >>>> : ");

            var searchGraduateId = Console.ReadLine();

            searchGraduateId = Regex.Replace(searchGraduateId.ToString(), "[^0-9a-zA-Z]+", "");

            if (searchGraduateId != String.Empty)
            {

                Graduate graduate = _contextGraduate.Graduate.SingleOrDefault(m => m.GraduateId == searchGraduateId);

                // MUST be on the graduate list
                if (graduate != null)
                {
                    // Status 1
                    // Finished with Display
                    if (graduate.Status != 1)
                    {
                        IEnumerable<Teleprompt> teleprompt = _contextTeleprompt.Teleprompt;
                        var isInTeleprompt = teleprompt.SingleOrDefault(m => m.GraduateId == searchGraduateId);
                        var countTeleprompt = teleprompt.Count();

                        // check and kick teleprompt screen, if there's a new reading
                        // so that after logics will always
                        // fulfill
                        if (isInTeleprompt != null && countTeleprompt > 0)
                        {
                            if (isInTeleprompt.Status == 1)
                            {
                                CleanTeleprompt(_contextTeleprompt);
                            }

                        }


                        // NOT in teleprompt
                        // AND
                        // EMPTY teleprompt
                        if (isInTeleprompt == null && countTeleprompt <= 0)
                        {
                            Console.WriteLine("\t\t\tTeleprompt Status: In: No, Count: {0}", countTeleprompt);

                            Queue queue = _contextQueue.Queue.SingleOrDefault(m => m.GraduateId == searchGraduateId);

                            // teleprompt is empty
                            // AND
                            // not in queue
                            if (queue == null)
                            {

                                CleanTeleprompt(_contextTeleprompt);

                                DisplayTeleprompt(_contextTeleprompt, searchGraduateId);
                               
                            }
                            else
                            {
                                CleanTeleprompt(_contextTeleprompt);

                                PopQueueAddTelepromptUpdateGraduate(_contextQueue, _contextTeleprompt, _contextGraduate);

                                Console.WriteLine("\t\t\tTeleprompt Status: In: No, Count: {0}", countTeleprompt);
                            }

                        }
                        else
                        {
                            // Teleprompt is not empty
                            // And 
                            // Is not the current display on screen
                            if (isInTeleprompt == null && countTeleprompt > 0)
                            {
                                // queue
                                IEnumerable<Queue> queue = _contextQueue.Queue;
                                var isInQueue = queue.SingleOrDefault(m => m.GraduateId == searchGraduateId);
                                if (isInQueue == null)
                                {
                                    _contextQueue.Queue.Add(new Queue() { GraduateId = searchGraduateId, Created = System.DateTime.Now });
                                    _contextQueue.SaveChanges();

                                    Console.WriteLine("\t\t\tQueue Status >> In: No, Count: {0}", queue.Count());
                                }
                                else
                                {
                                    Console.WriteLine("\t\t\tQueue Status >> In: Yes, Count: {0}", queue.Count());
                                }
                            }
                        }
                    } else
                    {
                        Console.WriteLine("\nGraduate Screen Time... Already finished!!!");
                        Console.WriteLine("Do you want to show this graduate again? Please change Graduate Status to 0\n");
                        
                    }

                }
                else
                {
                    // Not a Graduate Id string tag
                    Console.WriteLine("Can't find that Id @0", searchGraduateId);
                }



            }
            else
            {
                Console.WriteLine("Invalid string: @0", searchGraduateId);
            }


            Console.Write("Press any key to exit.");
            Console.ReadLine();

            return;

            
            try
            {
                reader.Connect(SolutionConstants.ReaderHostname);

                reader.TagsReported += OnTagsReported;

                reader.ApplyDefaultSettings();

                reader.Start();

                Console.WriteLine("Enter word. 'Yallah!' to equit.");

                string quitline = Console.ReadLine();

                if (quitline == "Yallah!")
                {
                    Console.WriteLine("\n\nExiting!!!");

                    reader.Stop();

                    reader.Disconnect();
                }


            }
            catch (OctaneSdkException e)
            {
                Console.WriteLine("Octane SDK exception: {0}", e.Message);

                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception : {0}", e.Message);

                Console.ReadLine();
            }

        }


        static void OnTagsReported(ImpinjReader sender, TagReport report)
        {
            // This event handler is called asynchronously 
            // when tag reports are available.
            // Loop through each tag in the report 
            // and print the data.
            foreach (Tag tag in report)
            {
                Console.WriteLine("EPC / GraduateId : {0} ", Regex.Replace(tag.Epc.ToString(), "[^0-9a-zA-Z]+", ""));

            }
        }

        static void CleanTeleprompt(TelepromptDbContext context)
        {
            // clean teleprompt
            foreach (var item in context.Teleprompt)
            {
                context.Teleprompt.Remove(item);
            }
            // save don't wait
            context.SaveChanges();
        }

        static void DisplayTeleprompt(TelepromptDbContext t, string searchGraduateId)
        {
            var teleprompt = new Teleprompt() { GraduateId = searchGraduateId, Created = DateTime.Now };
            t.Teleprompt.Add(teleprompt);
            t.SaveChanges();
        }


        static void PopQueueAddTelepromptUpdateGraduate(QueueDbContext q, TelepromptDbContext t, GraduateDbContext g)
        {
            // remove the top of the queue
            var itemTopQueue = q.Queue.OrderBy(m => m.Created).FirstOrDefault();
            if (itemTopQueue != null)
            {
                q.Remove(itemTopQueue);
                // save don't wait
                q.SaveChanges();

                // add to teleprompt
                var respectedTime = DateTime.Now.ToString();

                var teleprompt = new Teleprompt() { GraduateId = itemTopQueue.GraduateId, Created = itemTopQueue.Created };
                t.Teleprompt.Add(teleprompt);
                t.SaveChanges();

                var graduate = g.Graduate.FirstOrDefault(m => m.GraduateId == itemTopQueue.GraduateId);

                // save to the disk
                /* try
                {
                    var mainFilename = _hostEnvironment.WebRootPath + "\\" + "resources" + "\\" + "log" + "\\" + "log.txt";
                    using (var fs = new System.IO.FileStream(mainFilename, System.IO.FileMode.Append))
                    {
                        using (var stream = new System.IO.StreamWriter(fs))
                        {
                            stream.WriteLine(respectedTime + " Id:" + graduate.GraduateId + " Name:" + graduate.FirstName + " " + graduate.LastName);

                            stream.Flush();
                        }


                    }
                }
                catch (Exception e)
                {
                    Console.Write(e.Message);
                    // ignore any io error
                } */

                if (graduate != null)
                {
                    graduate.Status = 1;
                }
                // save don't wait
                g.SaveChanges();

            }
        }

        public void StandardSQLQueries()
        {

            // Defines the sources of configuration information for the 
            // application.
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            // Create the configuration object that the application will
            // use to retrieve configuration information.
            var configuration = builder.Build();

            // Retrieve the configuration information.
            var configValue = configuration.GetConnectionString("MainDisplayDB");


            var conn = new SqlConnection(configValue);


            if (conn != null && conn.State == ConnectionState.Closed)
            {
                conn.Open();

                if (conn.State == ConnectionState.Open)
                {
                    /* using (SqlCommand command = new SqlCommand("SELECT * FROM Queue WHERE GraduateId = @0", conn))
                    {
                        command.Parameters.Add(new SqlParameter("0", "112200165"));
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            Console.WriteLine("GraduateId \t | Created");
                            while (reader.Read())
                            {
                                Console.WriteLine(String.Format("{0} \t | {1}",
                                reader[0], reader[1]));
                            }
                        }
                    } */

                    /* Console.WriteLine("INSERT INTO command");
                   SqlCommand insertCommand = new SqlCommand("INSERT INTO TableName (FirstColumn, SecondColumn, ThirdColumn, ForthColumn) VALUES (@0, @1, @2, @3)", conn);
                   insertCommand.Parameters.Add(new SqlParameter("0", 10));
                   insertCommand.Parameters.Add(new SqlParameter("1", "Test Column"));
                   insertCommand.Parameters.Add(new SqlParameter("2", DateTime.Now));
                   insertCommand.Parameters.Add(new SqlParameter("3", false));

                   Console.WriteLine("Commands executed! Total rows affected are " + insertCommand.ExecuteNonQuery());
                   Console.WriteLine("Done! Press enter to move to the next step");
                   Console.ReadLine();
                   Console.Clear();
                   Console.WriteLine("Now the error trial!");

                   try
                   {
                       SqlCommand errorCommand = new SqlCommand("SELECT * FROM someErrorColumn", conn);
                       errorCommand.ExecuteNonQuery();
                   }
                   catch (SqlException er)
                   {
                       Console.WriteLine("There was an error reported by SQL Server, " + er.Message);
                   } */
                }
            }


            conn.Open();


            if (conn != null && conn.State == ConnectionState.Open)
            {


                using (SqlCommand command = new SqlCommand("SELECT GraduateId, FullName FROM Graduate WHERE GraduateId = @0", conn))
                {
                    // tag read here
                    // clean it up
                    // remove unnecessary characters
                    // like dashes and spaces
                    var tag = "0000000000---- 000001  ---- 01200  046";
                    // Console.WriteLine("GraduateId: " + Regex.Replace(tag, "[^0-9a-zA-Z]+", ""));
                    command.Parameters.Add(new SqlParameter("0", Regex.Replace(tag, "[^0-9a-zA-Z]+", "")));
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Console.WriteLine(String.Format("\t\tGraduateId: {0} \t Fullname: {1}", reader[0], reader[1]));
                        }
                    }
                }


                Console.WriteLine("Press any key to proceed with tag reading.");
                Console.ReadLine();
            }
        }
    }
}
