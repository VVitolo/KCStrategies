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
using NinjaTrader.NinjaScript.Indicators.CaminhoDolarizado;
#endregion

namespace NinjaTrader.NinjaScript.Strategies.KCStrategies
{
    public class CDScalping3 : KCAlgoBase
    {
        
        // Indicadores customizados
        private CDentries cdEntriesInd;
        private CDFORCE5 cdForceInd;
        // Controle de entrada e filtragem de tendência
        private int entryBarNumber = -1;
        private int macroTrendDirection = 0; // 1 = alta, -1 = baixa, 0 = indefinido

        #region Strategy Input Properties

        // Trade parameters
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Trade Quantity", Order = 1, GroupName = "Trade Parameters")]
        public int TradeQuantity { get; set; } = 1;

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Stop Loss Ticks", Order = 2, GroupName = "Trade Parameters")]
        public int StopLossTicks { get; set; } = 20;

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Profit Target Ticks", Order = 3, GroupName = "Trade Parameters")]
        public int ProfitTargetTicks { get; set; } = 40;

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Trailing Stop Ticks", Order = 4, GroupName = "Trade Parameters")]
        public int TrailingStopTicks { get; set; } = 0;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Bars Timeout", Order = 5, GroupName = "Trade Parameters")]
        public int BarsTimeout { get; set; } = 5;

        // ATR usado para cálculos auxiliares (ex.: trailing stop)
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ATR Period", Order = 6, GroupName = "Auxiliary")]
        public int ATRPeriod { get; set; } = 14;

        // Parâmetros para o CDFORCE5 (Macro Tendência)
        [NinjaScriptProperty]
        [Display(Name = "Force MomentumPeriod", Order = 7, GroupName = "CDFORCE5 Parameters")]
        public int ForceMomentumPeriod { get; set; } = 10;

        [NinjaScriptProperty]
        [Display(Name = "Force LinRegPeriod", Order = 8, GroupName = "CDFORCE5 Parameters")]
        public int ForceLinRegPeriod { get; set; } = 50;

        [NinjaScriptProperty]
        [Display(Name = "Force Medium Up Brush", Order = 9, GroupName = "CDFORCE5 Parameters")]
        public Brush ForceMediumUpBrush { get; set; } = Brushes.LightGreen;

        [NinjaScriptProperty]
        [Display(Name = "Force Medium Down Brush", Order = 10, GroupName = "CDFORCE5 Parameters")]
        public Brush ForceMediumDownBrush { get; set; } = Brushes.Orange;

        #endregion

        #region Auxiliary Indicators
        private EMA emaHigh20;
        private EMA emaLow20;
        private ATR atrIndicator;
        #endregion


		public override string DisplayName { get { return Name; } }
		
        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.SetDefaults)
            {
                Description = "Scalping strategy using CDentries (MicroST & EMA20 channel) and CDFORCE5 for macro trend filtering.";
                Name = "CD Scalping Strategy v.3.0";
                StrategyName = "CDScalpingStrategyV3";
                Version = "5.2 Apr. 2025";
                Credits = "Strategy by LeozeraTrader";
                ChartType =  "Tbars 12";	
				
//                InitialStop		= 140;
//				ProfitTarget	= 32;
            }
            else if (State == State.DataLoaded)
            {
                InitializeIndicators();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBars[0] < BarsRequiredToTrade)
                 if (CurrentBar < BarsRequiredToTrade)
                return;

            double currentClose = Close[0];
            double upperZone = cdForceInd.CDForceMediumZoneUpper;
            double lowerZone = cdForceInd.CDForceMediumZoneLower;

            // Atualiza a macrotendência baseada nos limiares do cdForceInd
            if (macroTrendDirection == 0)
            {
                if (currentClose > upperZone)
                    macroTrendDirection = 1;
                else if (currentClose < lowerZone)
                    macroTrendDirection = -1;
            }
            else if (macroTrendDirection == 1)
            {
                if (currentClose < lowerZone)
                    macroTrendDirection = -1;
            }
            else if (macroTrendDirection == -1)
            {
                if (currentClose > upperZone)
                    macroTrendDirection = 1;
            }

            // Determina a microtendência usando o cdEntriesInd
            bool microUp   = (Close[0] > cdEntriesInd.MicroST[0]) && (Close[0] > cdEntriesInd.EMA20High[0]);
            bool microDown = (Close[0] < cdEntriesInd.MicroST[0]) && (Close[0] < cdEntriesInd.EMA20Low[0]);

            // Gerencia timeout da posição
            if (Position.MarketPosition != MarketPosition.Flat)
            {
                if (entryBarNumber < 0)
                    entryBarNumber = CurrentBar;
                else if (CurrentBar - entryBarNumber >= BarsTimeout)
                {
                    if (Position.MarketPosition == MarketPosition.Long)
                        ExitLong("TimeoutExit", "EntriesLong");
                    else if (Position.MarketPosition == MarketPosition.Short)
                        ExitShort("TimeoutExit", "EntriesShort");
                }
            }
            else
            {
                entryBarNumber = -1;
            }

            // Condições de entrada: se flat e sinais macro e micro alinhados
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (macroTrendDirection == 1 && microUp)
                {
//                    EnterLong(TradeQuantity, "EntriesLong");
					longSignal = true;
                    entryBarNumber = CurrentBar;
                }
                else if (macroTrendDirection == -1 && microDown)
                {
//                    EnterShort(TradeQuantity, "EntriesShort");
					shortSignal = true;
                    entryBarNumber = CurrentBar;
                }
            }
			
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
            // Instancia do indicador CDentries (micro tendência)
            // ATENÇÃO: A assinatura esperada é:
            // CDentries(ISeries<double> input, int sTPeriod, double sTMultiplier, ATRMethod atrMethod, bool showMicroTrendArrow,
            //           Brush microSTUpColor, Brush microSTDownColor, Brush arrowUpColor, Brush arrowDownColor,
            //           CDMovingAverageType mABaselineType, int mABaselinePeriod, bool useMABaseline, int smooth,
            //           int ATRBandPeriod, double ATRBandMultiplier, bool showATRBands, bool showSweepArrows, int sweepLookBackPeriod)
            cdEntriesInd = CDentries(
                (ISeries<double>)Input,
                12,                        // sTPeriod
                2.0,                       // sTMultiplier
                ATRMethod.Wilder,          // ATRSmoothingMethod
                true,                      // ShowMicroTrendArrow
                Brushes.Green,             // MicroSTUpColor
                Brushes.Red,               // MicroSTDownColor
                Brushes.Blue,              // Arrow Up Color
                Brushes.OrangeRed,         // Arrow Down Color
                CDMovingAverageType.VMA,   // MABaselineType
                14,                        // MABaselinePeriod
                false,                     // Use MABaseline (se false, usa EMA(Smooth))
                14,                        // Smooth
                12,                        // ATRBandPeriod
                2.0,                       // ATRBandMultiplier
                true,                      // Show AT Bands
                true,                      // Show Sweep Arrows
                5,						   // SweepLookBackPeriod	
				false,
				0.41,
				-0.41,
				Brushes.Green,
				Brushes.Red
            );

            // Instancia do indicador CDFORCE5 (macro tendência)
            // Assinatura: CDFORCE5(int momentumPeriod, int linRegPeriod, ColorFilterMode filterMode,
            //                       bool paintBars, bool paintBackground, int bCopacity, Brush bColorUp, Brush bColorDn,
            //                       bool enableLowForceZone, double cDForceLowZoneUpper, double cDForceLowZoneLower,
            //                       bool enableMediumForceZone, double cDForceMediumZoneUpper, double cDForceMediumZoneLower,
            //                       bool enableHighForceZone, double cDForceHighZoneUpper, double cDForceHighZoneLower,
            //                       bool enablePrimeZone, double primeZoneUpper, double primeZoneLower,
            //                       double subprimeZoneUpper, double subprimeZoneLower,
            //                       bool enablePrimeReversalArrows, bool useSuperTrendOverride,
            //                       int sTPeriod, double sTMultiplier,
            //                       bool enableSuperMacroFilter, Brush macroTrendArrowUpColor, Brush macroTrendArrowDownColor,
            //                       bool useSimpleZoneCalculation, Brush upBrush, Brush downBrush, Brush mediumUpBrush, Brush mediumDownBrush)
            cdForceInd = CDFORCE5(
                ForceMomentumPeriod,      // momentumPeriod
                ForceLinRegPeriod,        // linRegPeriod
                ColorFilterMode.Default,  // filterMode
                true,                     // paintBars
                false,                    // paintBackground
                15,                       // bCopacity
                Brushes.Green,            // bColorUp
                Brushes.Red,              // bColorDn
                true,                     // enableLowForceZone
                0.123,                    // CDForceLowZoneUpper
                -0.123,                   // CDForceLowZoneLower
                true,                     // enableMediumForceZone
                0.41,                     // CDForceMediumZoneUpper
                -0.41,                    // CDForceMediumZoneLower
                true,                     // enableHighForceZone
                0.64,                     // CDForceHighZoneUpper
                -0.64,                    // CDForceHighZoneLower
                true,                     // enablePrimeZone
                0.28,                     // primeZoneUpper
                -0.28,                    // primeZoneLower
                0.19,                     // subprimeZoneUpper
                -0.19,                    // subprimeZoneLower
                true,                     // enablePrimeReversalArrows
                true,                     // useSuperTrendOverride
                12,                       // STPeriod (para SuperTrend Override)
                2.6,                      // STMultiplier (para SuperTrend Override)
                true,                     // enableSuperMacroFilter
                Brushes.Green,            // macroTrendArrowUpColor
                Brushes.Red,              // macroTrendArrowDownColor
                true,                     // useSimpleZoneCalculation
                Brushes.DarkGreen,        // upBrush
                Brushes.DarkRed,          // downBrush
                ForceMediumUpBrush,       // mediumUpBrush
                ForceMediumDownBrush      // mediumDownBrush
            );

            // Instancia indicadores auxiliares para visualização
            emaHigh20 = EMA(High, 20);
            emaLow20  = EMA(Low, 20);
            atrIndicator = ATR(ATRPeriod);

            AddChartIndicator(cdEntriesInd);
            AddChartIndicator(cdForceInd);
            entryBarNumber = -1;
            macroTrendDirection = 0;
        }
        #endregion

        #region Properties

        #endregion
    }
}
