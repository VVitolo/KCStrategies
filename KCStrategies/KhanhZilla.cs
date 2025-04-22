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
using NinjaTrader.NinjaScript.Strategies;
using RegressionChannel = NinjaTrader.NinjaScript.Indicators.RegressionChannel;
#endregion

namespace NinjaTrader.NinjaScript.Strategies.KCStrategies
{
    public class KhanhZilla : KCAlgoBase2
    {
		private double highestHigh;
		private double lowestLow;
		
		private RegressionChannelHighLow RegressionChannelHighLow1;	
		
		public override string DisplayName { get { return Name; } }
		
        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.SetDefaults)
            {
                Description = "Strategy based on the Linear Regression Channel indicator.";
                Name = "KhanhZilla v5.2";
                StrategyName = "KhanhZilla";
                Version = "5.2 Apr. 2025";
                Credits = "Strategy by Khanh Nguyen";
                ChartType = "30 Second Chart";		

				RegChanPeriod	= 20;
				RegChanWidth	= 4;
				
				InitialStop		= 97;
				
				ProfitTarget	= 60;
				ProfitTarget2	= 100;
				ProfitTarget3	= 140;
				ProfitTarget4	= 180;
				
            }
            else if (State == State.DataLoaded)
            {
                InitializeIndicators();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBars[0] < BarsRequiredToTrade)
                return;			
			
			longSignal = (Low[0] > RegressionChannelHighLow1.Lower[1]) 
				&& (Low[1] == RegressionChannelHighLow1.Lower[1]);

            shortSignal =  (High[0] < RegressionChannelHighLow1.Upper[1])
				&& (High[1] == RegressionChannelHighLow1.Upper[1]);
			
            base.OnBarUpdate();
        }

        protected override bool ValidateEntryLong()
        {
            // Logic for validating long entries
			if (longSignal) return true;
			else return false;
        }

        protected override bool ValidateEntryShort()
        {
            // Logic for validating short entries
			if (shortSignal) return true;
            else return false;
        }

       	protected override bool ValidateExitLong()
        {
            // Logic for validating long exits
            return enableExit? true : false;
        }

        protected override bool ValidateExitShort()
        {
			// Logic for validating short exits
			return enableExit? true : false;
        }

        #region Indicators
        protected override void InitializeIndicators()
        {
			RegressionChannelHighLow1 = RegressionChannelHighLow(Close, Convert.ToInt32(RegChanPeriod), RegChanWidth);		
			RegressionChannelHighLow1.Plots[0].Width = 2;
			RegressionChannelHighLow1.Plots[1].Width = 2;
			RegressionChannelHighLow1.Plots[2].Width = 2;
			AddChartIndicator(RegressionChannelHighLow1);
        }
        #endregion

        #region Properties

		[NinjaScriptProperty]
		[Display(Name="Regression Channel Period", Order=1, GroupName="08a. Strategy Settings")]
		public int RegChanPeriod
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="Regression Channel Width", Order=2, GroupName="08a. Strategy Settings")]
		public double RegChanWidth
		{ get; set; }
		
		#endregion

    }
}
