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
        static string configValue = String.Empty;

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
            configValue = configuration.GetConnectionString("MainDisplayDB");

            Console.WriteLine("a. The read tag will automatically be served to the Teleprompt Screen, if the screen is empty");
            Console.WriteLine("b. If the screen is not empty, await the read tag to queue until available");
            Console.WriteLine("c. If the screen is empty and there's queue waiting. Pop the queue and load to Teleprompt screen");
            
            try
            {
                reader.Connect(SolutionConstants.ReaderHostname);

                reader.TagsReported += OnTagsReported;

                reader.ApplyDefaultSettings();

                reader.Start();

                Console.WriteLine("\n\nEnter word. 'Yallah!' to quit.\n\n\n");

                string quitline = Console.ReadLine();

                if (quitline.ToLower() == "yallah!")
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
                TelepromptDbContext.ConnectionString = configValue;
                TelepromptDbContext _contextTeleprompt = new TelepromptDbContext();

                GraduateDbContext.ConnectionString = configValue;
                GraduateDbContext _contextGraduate = new GraduateDbContext();

                QueueDbContext.ConnectionString = configValue;
                QueueDbContext _contextQueue = new QueueDbContext();

                var searchGraduateId = Regex.Replace(tag.Epc.ToString(), "[^0-9a-zA-Z]+", "");
                
                Console.WriteLine("EPC / GraduateId >>>> {0} ", searchGraduateId);

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
                            var isSearchGraduateInTeleprompt = teleprompt.SingleOrDefault(m => m.GraduateId == searchGraduateId);
                            var countTeleprompt = teleprompt.Count();

                            // check and kick teleprompt screen, if there's a new reading
                            // so that after logics will always
                            // fulfill

                            // isSearchGraduateInTeleprompt is for the NEW Graduate Read
                            // NOTE: He's is not in the Teleprompt, someone is occupying it.
                            //       we need to kick the tenant, if he's already past his occupancy Status = 1
                            if (isSearchGraduateInTeleprompt == null && countTeleprompt > 0)
                            {
                                Teleprompt currentTenantOnTeleprompt = teleprompt.SingleOrDefault();

                                if (currentTenantOnTeleprompt.Status == 1)
                                {
                                    CleanTeleprompt(_contextTeleprompt);

                                    countTeleprompt = 0;
                                }

                            }

                            if (isSearchGraduateInTeleprompt != null && countTeleprompt > 0)
                            {
                                Console.WriteLine("\t\t\t\t\t <<<<<<<<<<<<<<< Active >>>>>>>>>>>>>>> ");
                            }


                            // NOT in teleprompt
                            // AND
                            // EMPTY teleprompt
                            if (isSearchGraduateInTeleprompt == null && countTeleprompt <= 0)
                            {
                                Console.WriteLine("\t\t\tTeleprompt Status: In: No, Count: {0}", countTeleprompt);

                                IEnumerable<Queue> queue = _contextQueue.Queue;
                                var isSearchGraduateInQueue = queue.SingleOrDefault(m => m.GraduateId == searchGraduateId);
                                var totalInQueue = queue.Count();

                                // teleprompt == empty
                                // searchGraduate not in queue
                                // queue == empty
                                if (isSearchGraduateInQueue == null && totalInQueue <= 0)
                                {
                                    // clean teleprompt
                                    CleanTeleprompt(_contextTeleprompt);

                                    // push directly to the teleprompt
                                    DisplayTeleprompt(_contextTeleprompt, searchGraduateId);

                                    // update graduate status
                                    var grad = _contextGraduate.Graduate.FirstOrDefault(m => m.GraduateId == searchGraduateId);
                                    if (grad != null)
                                    {
                                        grad.Status = 1;

                                        _contextGraduate.Update(grad);
                                        _contextGraduate.SaveChanges();
                                    }

                                }
                                else
                                {
                                    // queue is not empty and not in the queue
                                    // push in the end
                                    if (isSearchGraduateInQueue == null && totalInQueue > 0)
                                    {
                                        _contextQueue.Queue.Add(new Queue() { GraduateId = searchGraduateId, Created = System.DateTime.Now });
                                        _contextQueue.SaveChanges();

                                        Console.WriteLine("\t\t\t------------------------------------- In Queue");
                                    }

                                    CleanTeleprompt(_contextTeleprompt);

                                    PopQueueAddTelepromptUpdateGraduate(_contextQueue, _contextTeleprompt, _contextGraduate);

                                    Console.WriteLine("\t\t\tTeleprompt Status: In: No, Count: {0}", countTeleprompt);
                                    Console.WriteLine("\t\t\tQueue Status: In: Yes, Count: {0}", totalInQueue);

                                }

                            }
                            else
                            {
                                // Teleprompt is not empty
                                // And 
                                // Is not the current display on screen
                                if (isSearchGraduateInTeleprompt == null && countTeleprompt > 0)
                                {
                                    // queue
                                    IEnumerable<Queue> queue = _contextQueue.Queue;
                                    var isSearchGraduateInQueue = queue.SingleOrDefault(m => m.GraduateId == searchGraduateId);
                                    if (isSearchGraduateInQueue == null)
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
                        }
                        else
                        {
                            Console.WriteLine("\nGraduate Screen Time... Either FINISHED and/or ACTIVE !!!");
                            Console.WriteLine("Do you want to show this graduate again? Please change Graduate Status to 0\n");

                        }

                    }
                    else
                    {
                        // Not a Graduate Id string tag
                        Console.WriteLine("Can't find that Id {0}", searchGraduateId);
                    }



                }
                else
                {
                    Console.WriteLine("Invalid string: @0", searchGraduateId);
                }

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
            t.Add(teleprompt);
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
                t.Add(teleprompt);
                t.SaveChanges();

                var graduate = g.Graduate.FirstOrDefault(m => m.GraduateId == itemTopQueue.GraduateId);

                if (graduate != null)
                {
                    graduate.Status = 1;

                    g.Update(graduate);
                    g.SaveChanges();
                }

            }
        }
    }
}
