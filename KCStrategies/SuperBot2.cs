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
using BlueZ = NinjaTrader.NinjaScript.Indicators.BlueZ; // Alias for better readability
#endregion

namespace NinjaTrader.NinjaScript.Strategies.KCStrategies
{
  public class SuperBot2 : KCAlgoBase
  {
    #region Variables

    private LinReg2 LinReg1;
    private bool linRegUp;
    private bool linRegDown;

    private NinjaTrader.NinjaScript.Indicators.TradeSaber_SignalMod.TOWilliamsTraderOracleSignalMOD WilliamsR1;
    private bool WillyUp;
    private bool WillyDown;

    private CMO CMO1;
    private bool cmoUp;
    private bool cmoDown;

    private T3TrendFilter T3TrendFilter1;
    private double t3TrendValueUp;
    private double t3TrendValueDown;
    private bool t3TrendSignalUp;
    private bool t3TrendSignalDown;

    // Local indicators to avoid accessibility issues with parent class
    private NinjaTrader.NinjaScript.Indicators.RegressionChannel RegressionChannel1;
    private NinjaTrader.NinjaScript.Indicators.RegressionChannelHighLow RegressionChannelHighLow1;
    private BlueZ.BlueZHMAHooks hullMAHooks;
    private Momentum momentumIndicator;
    private bool hmaHooksUp;
    private bool hmaHooksDown;
    private bool regChanUp;
    private bool regChanDown;
    private bool longSignal;
    private bool shortSignal;

    // Store the chart type display string
    private string calculatedChartTypeDisplay = string.Empty;

    // Add state tracking variables
    private bool calculatedThisBar = false;
    private int lastCalculatedBar = -1;
    private SimpleFont statusFont;  // Add font as class field

    #endregion

    public override string DisplayName { get { return Name; } }

    protected override void OnStateChange()
    {
      base.OnStateChange();

      if (State == State.SetDefaults)
      {
        Description = "SuperBot is a combination of many strategies and indicators, including HMA Hooks, Linear Regression Channel, Momentum, and William R.";
        Name = "SuperBot2 v5.2";
        StrategyName = "SuperBot2";
        Version = "5.2 Apr. 2025";
        Credits = "Strategy by Khanh Nguyen";
        ChartType = "Orenko 34-40-40";

        HmaPeriod = 16;
        enableHmaHooks = true;
        showHmaHooks = true;

        LinRegPeriod = 9;
        enableLinReg = true;
        showLinReg = false;

        RegChanPeriod = 40;
        RegChanWidth = 4;
        RegChanWidth2 = 3;
        enableRegChan1 = true;
        enableRegChan2 = true;
        showRegChan1 = true;
        showRegChan2 = true;
        showRegChanHiLo = true;

        MomoUp = 5;
        MomoDown = -5;
        enableMomo = true;
        showMomo = true;

        CmoUp = 5;
        CmoDown = -5;
        enableCMO = true;
        showCMO = false;

        wrUp = -20;
        wrDown = -80;
        wrPeriod = 14;
        enableWilly = true;
        showWilly = false;

        // T3 Trend Filter settings
        Factor = 0.5;
        Period1 = 1;
        Period2 = 1;
        Period3 = 1;
        Period4 = 1;
        Period5 = 9;
        enableTrendy = true;
        showTrendy = false;

        // Initialize the font once
        statusFont = new SimpleFont("Arial", 10);
      }
      else if (State == State.DataLoaded)
      {
        InitializeIndicators();

        // Calculate chart type display string once
        try
        {
            string barTypeName = "Unknown";

            // Get all derived BarsType types and find matching one
            Type[] types = NinjaTrader.Core.Globals.AssemblyRegistry.GetDerivedTypes(typeof(BarsType));
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type == null || type.FullName.IsNullOrEmpty()) continue;
                var type2 = NinjaTrader.Core.Globals.AssemblyRegistry.GetType(type.FullName);
                if (type2 == null) continue;
                BarsType bar = Activator.CreateInstance(type2) as BarsType;
                if (bar != null)
                {
                    bar.SetState(State.SetDefaults);
                    int id = (int)bar.BarsPeriod.BarsPeriodType;
                    // If this bar type matches our current bars period type
                    if (id == (int)Bars.BarsPeriod.BarsPeriodType)
                    {
                        barTypeName = bar.Name;
                        bar.SetState(State.Terminated);
                        break;
                    }
                    bar.SetState(State.Terminated);
                }
            }

            // Build display string with the accurate name
            string displayString = $"{barTypeName} {Bars.BarsPeriod.Value}";

            // Add BaseBarsPeriodValue if it exists and is different from Value
            if (Bars.BarsPeriod.BaseBarsPeriodValue > 0 &&
                Bars.BarsPeriod.BaseBarsPeriodValue != Bars.BarsPeriod.Value)
            {
                displayString += $"-{Bars.BarsPeriod.BaseBarsPeriodValue}";
            }

            // Add Value3 if it exists and is greater than 0
            if (Bars.BarsPeriod.GetType().GetProperty("Value3") != null)
            {
                var value3Property = Bars.BarsPeriod.GetType().GetProperty("Value3");
                if (value3Property != null)
                {
                    var value3 = (int)value3Property.GetValue(Bars.BarsPeriod);
                    if (value3 > 0)
                    {
                        displayString += $"-{value3}";
                    }
                }
            }

            // Add MarketDataType if relevant
            if (Bars.BarsPeriod.MarketDataType != MarketDataType.Unknown)
            {
                displayString += $" ({Bars.BarsPeriod.MarketDataType})";
            }

            calculatedChartTypeDisplay = displayString;
            LogMessage($"Chart type calculated: {calculatedChartTypeDisplay}", "INIT");
        }
        catch (Exception ex)
        {
            LogError("Failed to calculate chart type display", ex);
            calculatedChartTypeDisplay = "Unknown Chart Type";
        }
      }
    }

    protected override void OnBarUpdate()
    {
      if (CurrentBars[0] < BarsRequiredToTrade)
        return;

      // Only calculate signals once per bar
      if (lastCalculatedBar == CurrentBar)
      {
        return;
      }

      // Call base class OnBarUpdate first to handle trade delay and other checks
      base.OnBarUpdate();

      // Only calculate full indicator signals if we're flat (looking for entries) or if exit checking is enabled
      bool needFullSignals = isFlat || (enableExit && (isLong || isShort));

      // Always update these base indicators as they're used for visualization/logging
      t3TrendValueUp = T3TrendFilter1.Values[0][0];
      t3TrendValueDown = T3TrendFilter1.Values[1][0];

      int longSignals = 0;
      int shortSignals = 0;

      if (needFullSignals && !calculatedThisBar)
      {
        bool channelSlopeUp = (RegressionChannel1.Middle[1] > RegressionChannel1.Middle[2]) && (RegressionChannel1.Middle[2] <= RegressionChannel1.Middle[3])
          || (RegressionChannel1.Middle[0] > RegressionChannel1.Middle[1] && Low[0] > Low[2] && Low[2] <= RegressionChannel1.Lower[2]);
        bool priceNearLowerChannel = (Low[0] > RegressionChannelHighLow1.Lower[2]);

        bool channelSlopeDown = (RegressionChannel1.Middle[1] < RegressionChannel1.Middle[2]) && (RegressionChannel1.Middle[2] >= RegressionChannel1.Middle[3])
          || (RegressionChannel1.Middle[0] < RegressionChannel1.Middle[1] && High[0] < High[2] && High[2] >= RegressionChannel1.Upper[2]);
        bool priceNearUpperChannel = (High[0] < RegressionChannelHighLow1.Upper[2]);

        regChanUp = enableRegChan1 ? channelSlopeUp || priceNearLowerChannel : true;
        regChanDown = enableRegChan1 ? channelSlopeDown || priceNearUpperChannel : true;

        // Ensure HMA Hooks signals are mutually exclusive
        hmaHooksUp = !enableHmaHooks || ((Close[0] > hullMAHooks[0] && hullMAHooks.trend[0] == 1 && hullMAHooks.trend[1] == -1)
          || (hullMAHooks[0] > hullMAHooks[1]));
        hmaHooksDown = !hmaHooksUp && !enableHmaHooks || ((Close[0] < hullMAHooks[0] && hullMAHooks.trend[0] == -1 && hullMAHooks.trend[1] == 1)
          || (hullMAHooks[0] < hullMAHooks[1]));

        // Ensure momentum signals are mutually exclusive
        momoUp = enableMomo ? momentumIndicator[0] > MomoUp && momentumIndicator[0] > momentumIndicator[1] : true;
        momoDown = !momoUp && enableMomo ? momentumIndicator[0] < MomoDown && momentumIndicator[0] < momentumIndicator[1] : true;

        // Ensure Williams %R signals are mutually exclusive
        WillyUp = enableWilly ? WilliamsR1[1] >= wrUp && Close[0] > Close[1] && High[1] > High[2] : true;
        WillyDown = !WillyUp && enableWilly ? WilliamsR1[1] <= wrDown && Close[0] < Close[1] && Low[1] < Low[2] : true;

        // Ensure CMO signals are mutually exclusive
        cmoUp = !enableCMO || CMO1[0] >= CmoUp;
        cmoDown = !cmoUp && !enableCMO || CMO1[0] <= CmoDown;

        // Ensure LinReg signals are mutually exclusive
        linRegUp = !enableLinReg || LinReg1[0] > LinReg1[2];
        linRegDown = !linRegUp && !enableLinReg || LinReg1[0] < LinReg1[2];

        // Ensure T3 Trend signals are mutually exclusive
        t3TrendSignalUp = !enableTrendy || (t3TrendValueUp >= 5 && t3TrendValueDown == 0);
        t3TrendSignalDown = !t3TrendSignalUp && !enableTrendy || (t3TrendValueDown <= -5 && t3TrendValueUp == 0);

        // Final signal calculation - require at least two confirming signals
        longSignals = (t3TrendSignalUp ? 1 : 0) + (momoUp ? 1 : 0) + (linRegUp ? 1 : 0) +
                         (cmoUp ? 1 : 0) + (hmaHooksUp ? 1 : 0) + (regChanUp ? 1 : 0);
        shortSignals = (t3TrendSignalDown ? 1 : 0) + (momoDown ? 1 : 0) + (linRegDown ? 1 : 0) +
                          (cmoDown ? 1 : 0) + (hmaHooksDown ? 1 : 0) + (regChanDown ? 1 : 0);

        longSignal = longSignals >= 2;  // Require at least 2 confirming signals
        shortSignal = shortSignals >= 2; // Require at least 2 confirming signals

        // Log indicator states
        if (State == State.Realtime)
        {
          LogMessage($"Bar {CurrentBar} | " +
                    $"RegChan[{(regChanUp ? "↑" : regChanDown ? "↓" : "−")}] | " +
                    $"HMA[{(hmaHooksUp ? "↑" : hmaHooksDown ? "↓" : "−")}] | " +
                    $"Momo[{(momoUp ? "↑" : momoDown ? "↓" : "−")},{momentumIndicator[0]:F1}] | " +
                    $"Willy[{(WillyUp ? "↑" : WillyDown ? "↓" : "−")},{WilliamsR1[0]:F1}] | " +
                    $"CMO[{(cmoUp ? "↑" : cmoDown ? "↓" : "−")},{CMO1[0]:F1}] | " +
                    $"LinReg[{(linRegUp ? "↑" : linRegDown ? "↓" : "−")},{LinReg1[0]:F1}] | " +
                    $"T3[{(t3TrendSignalUp ? "↑" : t3TrendSignalDown ? "↓" : "−")},{t3TrendValueUp:F1}/{t3TrendValueDown:F1}] | " +
                    $"SIGNAL[{(longSignal ? "LONG" : shortSignal ? "SHORT" : "NEUTRAL")}] | " +
                    $"Confirming Signals: Long={longSignals}, Short={shortSignals}", "STATE");
        }

        calculatedThisBar = true;
        lastCalculatedBar = CurrentBar;
      }
      else if (!needFullSignals && State == State.Realtime)
      {
        // Log minimal state info when in position
        LogMessage($"Bar {CurrentBar} | In {(isLong ? "LONG" : isShort ? "SHORT" : "FLAT")} position - Skipping signal calculations", "STATE");
      }

      // Main signals and status panel (top left - SuperBot2 specific)
      string strategySignalsText =
          $"\n\n" +
          $"=== SUPERBOT2 SIGNALS ===\n" +
          $"Chart Type: {calculatedChartTypeDisplay}\n" +
          $"{"HMA Hooks:",-12} {(needFullSignals ? (hmaHooksUp ? "↑" : hmaHooksDown ? "↓" : "−") : "N/A"),-2} [Period: {HmaPeriod,2}, Enabled: {enableHmaHooks}]\n" +
          $"{"Momentum:",-12} {momentumIndicator[0],4:F1} [Up: {MomoUp,2}, Down: {MomoDown,3}, Enabled: {enableMomo}]\n" +
          $"{"Williams %R:",-12} {WilliamsR1[0],5:F1} [Period: {wrPeriod,2}, Up: {wrUp,3}, Down: {wrDown,3}, Enabled: {enableWilly}]\n" +
          $"{"CMO:",-12} {CMO1[0],4:F1} [Up: {CmoUp,2}, Down: {CmoDown,3}, Enabled: {enableCMO}]\n" +
          $"{"LinReg:",-12} {(needFullSignals ? (linRegUp ? "↑" : linRegDown ? "↓" : "−") : "N/A"),-2} [Period: {LinRegPeriod,2}, Enabled: {enableLinReg}]\n" +
          $"{"T3 Trend:",-12} {(needFullSignals ? (t3TrendSignalUp ? "↑" : t3TrendSignalDown ? "↓" : "−") : "N/A"),-2} [Enabled: {enableTrendy}]\n" +
          $"{"RegChan:",-12} {(needFullSignals ? (regChanUp ? "↑" : regChanDown ? "↓" : "−") : "N/A"),-2} [Period: {RegChanPeriod,2}, Width: {RegChanWidth:F0}/{RegChanWidth2:F0}]\n" +
          $"\n=== STRATEGY STATUS ===\n" +
          $"{"Signal:",-12} {(needFullSignals ? (longSignal ? "LONG" : shortSignal ? "SHORT" : "NEUTRAL") : "IN POSITION"),-7}\n" +
          $"{"Position:",-12} {(isLong ? "LONG" : isShort ? "SHORT" : "FLAT"),-7}\n" +
          $"{"Auto:",-12} {(isAutoEnabled ? "ON" : "OFF"),-3}\n" +
          $"{"Direction:",-12} Long {(isLongEnabled ? "ON" : "OFF"),-3} / Short {(isShortEnabled ? "ON" : "OFF"),-3}\n" +
          $"\n=== CONFIRMING SIGNALS ===\n" +
          $"Long Signals: {longSignals}\n" +
          $"Short Signals: {shortSignals}";

      // Draw strategy-specific panel at top left
      if (ShowStatusPanels)
      {
          Draw.TextFixed(this, "strategy_signals", strategySignalsText, TextPosition.TopLeft,
              Brushes.White, statusFont, null, Brushes.Black, 100, DashStyleHelper.Solid, 0, false, "");

          // Call base class to draw its status panel at bottom left
          base.DrawStrategyStatus();
      }
      else
      {
          // Remove the panels if they exist and ShowStatusPanels is false
          RemoveDrawObject("strategy_signals");
      }
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
      return enableExit ? true : false;
    }

    protected override bool ValidateExitShort()
    {
      // Logic for validating short exits
      return enableExit ? true : false;
    }

    #region Indicators
    protected override void InitializeIndicators()
    {
      try {
        LogMessage("Initializing SuperBot2 indicators...", "INIT");

        // Initialize and track all indicators in a single log line
        string initLog = "";

        // HMA Hooks
        hullMAHooks = BlueZHMAHooks(Close, HmaPeriod, 0, false, false, true, Brushes.Lime, Brushes.Red);
        hullMAHooks.Plots[0].Brush = Brushes.White;
        hullMAHooks.Plots[0].Width = 2;
        if (showHmaHooks && enableHmaHooks) {
          AddChartIndicator(hullMAHooks);
          initLog += "HMA✓ ";
        }

        // Regression Channels
        RegressionChannel1 = RegressionChannel(Close, RegChanPeriod, RegChanWidth);
        RegressionChannelHighLow1 = RegressionChannelHighLow(Close, RegChanPeriod, RegChanWidth);
        if (showRegChan1 && enableRegChan1) {
          AddChartIndicator(RegressionChannel1);
          initLog += "RegChan1✓ ";
        }
        if (showRegChanHiLo) {
          AddChartIndicator(RegressionChannelHighLow1);
          initLog += "RegChanHL✓ ";
        }

        // Momentum
        momentumIndicator = Momentum(Close, 14);
        momentumIndicator.Plots[0].Brush = Brushes.Yellow;
        momentumIndicator.Plots[0].Width = 2;
        if (showMomo && enableMomo) {
          AddChartIndicator(momentumIndicator);
          initLog += "Momo✓ ";
        }

        // Linear Regression
        LinReg1 = LinReg2(Close, LinRegPeriod);
        LinReg1.Plots[0].Width = 2;
        if (showLinReg && enableLinReg) {
          AddChartIndicator(LinReg1);
          initLog += "LinReg✓ ";
        }

        // Williams %R
        WilliamsR1 = TOWilliamsTraderOracleSignalMOD(Close, wrPeriod, @"LongEntry", @"ShortEntry");
        WilliamsR1.Plots[0].Brush = Brushes.Yellow;
        WilliamsR1.Plots[0].Width = 1;
        if (showWilly && enableWilly) {
          AddChartIndicator(WilliamsR1);
          initLog += "Willy✓ ";
        }

        // CMO
        CMO1 = CMO(Close, 14);
        CMO1.Plots[0].Brush = Brushes.Yellow;
        CMO1.Plots[0].Width = 2;
        if (showCMO && enableCMO) {
          AddChartIndicator(CMO1);
          initLog += "CMO✓ ";
        }

        // T3 Trend Filter
        T3TrendFilter1 = T3TrendFilter(Close, Factor, Period1, Period2, Period3, Period4, Period5, false);
        if (showTrendy && enableTrendy) {
          AddChartIndicator(T3TrendFilter1);
          initLog += "T3✓";
        }

        LogMessage($"Indicators initialized: {initLog}", "INIT");
      }
      catch (Exception ex) {
        LogError("Failed to initialize SuperBot2 indicators", ex);
        throw;
      }
    }
    #endregion

    #region Properties

    // Properties specific to SuperBot2 that are not in KCAlgoBase
    [NinjaScriptProperty]
    [Display(Name = "Enable Linear Regression", Order = 1, GroupName = "08a. Strategy Settings")]
    public bool enableLinReg { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Show Linear Regression", Order = 2, GroupName = "08a. Strategy Settings")]
    public bool showLinReg { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Linear Regression Period", Order = 3, GroupName = "08a. Strategy Settings")]
    public int LinRegPeriod { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable Williams %R", Order = 4, GroupName = "08a. Strategy Settings")]
    public bool enableWilly { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Show Williams %R", Order = 5, GroupName = "08a. Strategy Settings")]
    public bool showWilly { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Williams %R Period", Order = 6, GroupName = "08a. Strategy Settings")]
    public int wrPeriod { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Williams %R Up Level", Order = 7, GroupName = "08a. Strategy Settings")]
    public int wrUp { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Williams %R Down Level", Order = 8, GroupName = "08a. Strategy Settings")]
    public int wrDown { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable CMO", Order = 9, GroupName = "08a. Strategy Settings")]
    public bool enableCMO { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Show CMO", Order = 10, GroupName = "08a. Strategy Settings")]
    public bool showCMO { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "CMO Up Level", Order = 11, GroupName = "08a. Strategy Settings")]
    public int CmoUp { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "CMO Down Level", Order = 12, GroupName = "08a. Strategy Settings")]
    public int CmoDown { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable T3 Trend Filter", Order = 13, GroupName = "08a. Strategy Settings")]
    public bool enableTrendy { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Show T3 Trend Filter", Order = 14, GroupName = "08a. Strategy Settings")]
    public bool showTrendy { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "T3 Factor", Order = 15, GroupName = "08a. Strategy Settings")]
    public double Factor { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "T3 Period 1", Order = 16, GroupName = "08a. Strategy Settings")]
    public int Period1 { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "T3 Period 2", Order = 17, GroupName = "08a. Strategy Settings")]
    public int Period2 { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "T3 Period 3", Order = 18, GroupName = "08a. Strategy Settings")]
    public int Period3 { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "T3 Period 4", Order = 19, GroupName = "08a. Strategy Settings")]
    public int Period4 { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "T3 Period 5", Order = 20, GroupName = "08a. Strategy Settings")]
    public int Period5 { get; set; }

    #endregion
  }
}
