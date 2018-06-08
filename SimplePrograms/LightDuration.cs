using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SimplePrograms
{
    class LightDuration
    {
        static string entrance = "enter";
        static string exit = "exit";

        public static void Duration(string event_log)
        {
            int total_duration = 0;
            int visitors = 0;
            int visit_time;

            Tuple<int, string> visit_event;
            var visits = new List<Tuple<int, string>>();
            var visit_type = LightDuration.entrance;

            var events = event_log.Split(null);
            foreach (string ev in events)
            {
                if (int.TryParse(ev, out int event_time))
                {
                    visit_event = Tuple.Create(event_time, visit_type);
                    visits.Add(visit_event);
                    if (visit_type == entrance) visit_type = exit;
                    else if (visit_type == exit) visit_type = entrance;
                }
            }
            visits = visits.OrderBy(x => x.Item1).ToList();
            var visit_start = LightDuration.GetStartingVisit(visits);
            foreach (var v in visits)
            {
                visit_type = v.Item2;
                visit_time = v.Item1;
                if (visit_type == entrance) visitors += 1;
                else if (visit_type == exit) visitors = Math.Max(visitors - 1, 0);

                if (visitors == 0 & visit_type == exit) total_duration += (visit_time - visit_start);
                else if (visitors == 1 & visit_type == entrance) visit_start = visit_time;

            }
            if (total_duration == 1)
                Console.WriteLine(String.Format("The light was on for {0} minute.", total_duration));
            else
                Console.WriteLine(String.Format("The light was on for {0} minutes.", total_duration));
            Console.WriteLine("Press enter to exit.");
            Console.ReadLine();


        }
    
        static int GetStartingVisit(List<Tuple<int, string>> visits)
        {
            // Only works when visits are already sorted
            foreach (var visit in visits)
            {
                if (visit.Item2 == LightDuration.entrance) return visit.Item1;
            }
            throw new ArgumentException("Could not find a valid entrance time");

        }

    }
}
