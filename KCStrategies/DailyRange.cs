#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.NinjaScript;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

/*
		2.0.1:
			- Initial version

*/


//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class DailyRange : Strategy
    {
        private double dailyHigh;
        private double dailyLow;
        private List<double> dailyRanges = new List<double>(); // Stores daily ranges
        private int lookbackPeriod = 30; // Number of days for the average
        private string textTag = "DailyRangeText"; // Unique tag for drawing text on the chart
        private double tickSize = 0.25; // NQ tick size

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                // Set strategy properties
                Description = "Strategy to calculate the daily range of NQ, 30-day average range, and print points/ticks on the chart";
                Name = "Daily Range NQ With Points And Ticks";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;  // Allows us to draw on top of the chart
            }
            else if (State == State.DataLoaded)
            {
                // Initialize daily high and low to extreme values
                dailyHigh = double.MinValue;
                dailyLow = double.MaxValue;
            }
        }

        protected override void OnBarUpdate()
        {
            // Check if the trading session has ended (daily reset logic)
            if (Bars.IsFirstBarOfSession)
            {
                // Calculate the daily range from the previous session
                double dailyRange = dailyHigh - dailyLow;

                // Add the daily range to the list of ranges
                dailyRanges.Add(dailyRange);

                // Keep the list at the lookback period length (30 days)
                if (dailyRanges.Count > lookbackPeriod)
                {
                    dailyRanges.RemoveAt(0); // Remove the oldest range
                }

                // Calculate the 30-day average range
                double averageRange = 0;
                if (dailyRanges.Count > 0)
                {
                    averageRange = CalculateAverageRange();
                }

                // Calculate points and ticks for daily and average ranges
                double dailyRangePoints = dailyRange;
                double dailyRangeTicks = dailyRangePoints / tickSize;
                double averageRangePoints = averageRange;
                double averageRangeTicks = averageRangePoints / tickSize;

                // Print or store the daily range and the average range
                Print("Daily Range (NQ): " + dailyRangePoints + " points (" + dailyRangeTicks + " ticks)");
                Print("30-Day Average Range (NQ): " + averageRangePoints + " points (" + averageRangeTicks + " ticks)");

                // Reset the daily high and low for the new session
                dailyHigh = double.MinValue;
                dailyLow = double.MaxValue;

                // Update the chart text with points and ticks
                DrawTextOnChart(dailyRangePoints, dailyRangeTicks, averageRangePoints, averageRangeTicks);
            }

            // Update the daily high and low with the current bar values
            if (High[0] > dailyHigh)
                dailyHigh = High[0];

            if (Low[0] < dailyLow)
                dailyLow = Low[0];
        }

        // Helper method to calculate the average daily range
        private double CalculateAverageRange()
        {
            double sum = 0;
            foreach (double range in dailyRanges)
            {
                sum += range;
            }
            return sum / dailyRanges.Count;
        }

        // Method to draw text on the chart displaying the daily range and 30-day average
        // including points and ticks
        private void DrawTextOnChart(double dailyRangePoints, double dailyRangeTicks, double averageRangePoints, double averageRangeTicks)
        {
            // Clear the previous text
            RemoveDrawObject(textTag);

            // Draw new text at the top-left corner of the chart
            Draw.Text(this, textTag, 
                "Daily Range: " + dailyRangePoints.ToString("F2") + " points (" + dailyRangeTicks.ToString("F0") + " ticks)" + 
                "\n30-Day Avg Range: " + averageRangePoints.ToString("F2") + " points (" + averageRangeTicks.ToString("F0") + " ticks)",
                0, High[0] + TickSize * 10, Brushes.White);
        }
    }
}
