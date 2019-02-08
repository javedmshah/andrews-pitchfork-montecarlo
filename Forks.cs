#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class Forks : Strategy
	{
		bool changeHigh = false;
		bool changeMajorLow = false;
		bool changeRecentLow = false;
		int counter = 0;
		int dotCounter = 0;
		bool drawFork = false;
		bool downTrend = false;

		double minLowRecent;
		double minLow;
		double maxHigh;
		int minLowBarsAgo;
		int minLowRecentBarsAgo;
		int maxHighBarsAgo;
		int medianBarsAgo;
		int medianHBarsAgo;
		int medianLBarsAgo;
		double prevMaxHigh;
		double prevMinLow;
		double prevMinLowRecent;
		int prevMaxHighBarsAgo=-1;
		int prevMedianBarsAgo=-1;
		int prevMinLowBarsAgo=-1;
		int prevMinLowRecentBarsAgo=-1;
		int mediansRecordedBarsAgo=-1;
		double[] mLine;
		double[] hmLine;
		double[] lmLine;
		double[] mlmLine;
		double[] mhmLine;
		private Order forkOrder								= null;
		int barCounter;
		private Order entryOrder						= null; // This variable holds an object representing our entry order
		private Order stopOrder                         = null; // This variable holds an object representing our stop loss order
		private Order targetOrder                       = null; // This variable holds an object representing our profit target order
		private int barNumberOfOrder 					= 0;	// This variable is used to store the entry bar
		private int priorTradesCount = 0;
		private double priorTradesCumProfit = 0;
		bool modifiedSchiff = false;
		int delta = 0;
		string forkId = "";
		float forkSlope = 0;
		double forkWidth = 0;
		double zeroLine = 0;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Strategy here.";
				Name										= "Forks";
				Calculate									= Calculate.OnBarClose;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.AdoptAccountPosition;
				TimeInForce									= TimeInForce.Gtc;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 10;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= false;
				LIMIT = 3;
				DOTLIMIT = 6;
				WIDTH = 10;
				FORK_LENGTH = 200;
				PROFIT_TARGET = 500;
				FORK_WIDTH = 10;
				HIGH_STRENGTH = 15;
				STOP_VALUE = 500;
				DRAW_TRIANGLES = true;
				drawDots = true;
				useATRStops = true;
				atMarket = true;
				breakEvenStop = false;
				SLOPE_LIMIT_MSFK = 3;
			}
			else if (State == State.Realtime)
			{
				// one time only, as we transition from historical
			    // convert any old historical order object references
			    // to the new live order submitted to the real-time account
			    if (entryOrder != null)
			        entryOrder = GetRealtimeOrder(entryOrder);
				if (stopOrder != null)
			        stopOrder = GetRealtimeOrder(stopOrder);
				if (targetOrder != null)
			        targetOrder = GetRealtimeOrder(targetOrder);
			}
			else if (State == State.Historical)
			{
				/*Core.Globals.RandomDispatcher.BeginInvoke(new Action(() => 
				{ 
				 	NinjaTrader.Gui.NinjaScript.NinjaScriptOutput newNsOutput = new NinjaTrader.Gui.NinjaScript.NinjaScriptOutput();
					newNsOutput.Show(); 
					newNsOutput.Activate();
				}));*/
			} 
			else if (State == State.DataLoaded)
			{
				mLine = new double[FORK_LENGTH+1];
				mLine[0] = -1;
				hmLine = new double[FORK_LENGTH+1];
				hmLine[0] = -1;
				lmLine = new double[FORK_LENGTH+1];
				lmLine[0] = -1;
				mlmLine = new double[FORK_LENGTH+1];
				mlmLine[0] = -1;
				mhmLine = new double[FORK_LENGTH+1];
				mhmLine[0] = -1;
				minLow = 9999;
				maxHigh = 0;
				minLowRecent = 9999;
				minLowBarsAgo = 0;
				minLowRecentBarsAgo = 0;
				maxHighBarsAgo = 0;				
				mediansRecordedBarsAgo = -1;
				barCounter = -99; // no increment if -99, only if 0
				Draw.TextFixed(this, "x", ""+ (1/TickSize), TextPosition.TopRight);
				Draw.TextFixed(this, "y", ""+ Bars.Instrument.MasterInstrument.PointValue, TextPosition.BottomRight);
			}
		}

		protected override void OnBarUpdate()
		{
			try {
				
				//Add your custom strategy logic here.
				if( CurrentBar < HIGH_STRENGTH*2)
					return;
				//Draw.Text(this, "cb"+dotCounter, ""+CurrentBar, 0, High[0]);
				//dotCounter++;
				//if(ToTime(Time[0]) < 95600 && ToTime(Time[0]) > 124500) {
				//	return;
				//}
				// At the start of a new session
				if (Bars.IsFirstBarOfSession)
				{
					// Store the strategy's prior cumulated realized profit and number of trades
					priorTradesCount = SystemPerformance.AllTrades.Count;
					priorTradesCumProfit = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;

					/* NOTE: Using .AllTrades will include both historical virtual trades as well as real-time trades.
					If you want to only count profits from real-time trades please use .RealtimeTrades. */
				}
				// cancel pending orders at session close
				if(Bars.IsLastBarOfSession) {
					if(entryOrder != null)
					{
						Draw.ArrowDown(this, "o"+dotCounter, true, 0, Close[0], Brushes.Red);
						Draw.Text(this, "cl"+dotCounter, ""+entryOrder.Name, 0, Low[0], Brushes.Red);
	                    dotCounter++;
						CancelOrder(entryOrder);
						entryOrder = null;						
					}
				}
				
				
				// 
				if(barCounter != -99)
					barCounter++;
				if(barCounter > FORK_LENGTH) {
					// need to draw a new one 
					prevMinLowBarsAgo = -1;
					prevMinLowRecentBarsAgo = -1;
					maxHigh = -1;
					prevMaxHigh = -1;
					maxHighBarsAgo = -1;
					prevMaxHighBarsAgo = -1;
					minLowRecentBarsAgo = -1;
					mLine[0] = -1;
					lmLine[0] = -1;
					hmLine[0] = -1;
					mhmLine[0] = -1;
					mlmLine[0] = -1;
					barCounter = -99;
					changeMajorLow = false;
					changeHigh = false;
					changeRecentLow = false;
				}
				double atr = Math.Round(ATR(WIDTH)[0]);
				
				// get me the highest high since a N prior high from total HIGH_STRENGTH bars ago
				prevMaxHighBarsAgo = (int)maxHighBarsAgo;
				prevMaxHigh = maxHigh;
				maxHigh = High[HighestBar(High,Math.Max(prevMaxHighBarsAgo, HIGH_STRENGTH))]; 
				// lets find how many bars ago
				for(int i=0; i<Math.Max(prevMaxHighBarsAgo, HIGH_STRENGTH); i++) {
					if(maxHigh == High[i]) {
						maxHighBarsAgo = i;
					}
				}
				
				if(CurrentBar >= 1350) {
					float whatever = 0;

				}
				// ensure this is a different value
				if(prevMaxHigh != maxHigh) {
					//if(barCounter != -1 && maxHigh > lmLine[0]) {
						changeHigh = true;
						if(drawDots) 
							Draw.Dot(this, "dot"+dotCounter, true, (int) maxHighBarsAgo, maxHigh, Brushes.Green);
						
					//}
				}

				// get me the lowest low since the prior low from total 20 bars ago
				// start the computation from the bar number we recorded a major high above
				//if(prevMinLow != minLow)
					prevMinLow = minLow;
				//if(prevMinLowBarsAgo != minLowBarsAgo)
					prevMinLowBarsAgo = (int) minLowBarsAgo;
				// find the bars ago for this major low candidate
				minLow = Low[(int)maxHighBarsAgo+1];
				minLowBarsAgo = maxHighBarsAgo+1;
				for(int i=(int)maxHighBarsAgo+1; i<(int)maxHighBarsAgo+HIGH_STRENGTH; i++) {				
					if (minLow >= Low[i]) {
						minLow = Low[i];
						minLowBarsAgo = i;
					}
				}
				// only makes sense to get a major low if major high changed, right?
				if(changeHigh) {
					// this is a different value
					if(minLow != prevMinLow ) {	
						//if(barCounter != -1 && minLow < lmLine[0]) {
							changeMajorLow = true;
							if(drawDots) 
								Draw.Dot(this, "dml"+dotCounter, true, (int)minLowBarsAgo, minLow, Brushes.Red);
							
						//}
					}
				}
				
				// get me the lowest low since the maxHigh occurred
				//if(prevMinLowRecent != minLowRecent)
					prevMinLowRecent = minLowRecent;
				//if(prevMinLowRecentBarsAgo != minLowRecent)
					prevMinLowRecentBarsAgo = minLowRecentBarsAgo;
					
				minLowRecent = Low[LowestBar(Low, maxHighBarsAgo-1)];
				// now find the bars ago this recent low occurred after the maxHigh
				for(int i=0; i<(int)maxHighBarsAgo-1; i++) {	
					if(minLowRecent == Low[i]) {
						minLowRecentBarsAgo = i;
					}
				}
				if(prevMinLowRecent != minLowRecent) {
					changeRecentLow = true;
				}
				
				downTrend = (Math.Abs(minLowRecent - minLow) > (maxHigh - minLow)) ? true : false;
				
				// only use this low if it makes for a good fork
				if(//changeHigh && changeMajorLow				
					// dont draw steep uptrend forks
					//&& (minLowRecent <= (maxHigh+minLow)/2)
					// dont allow recent low higher than maxHigh
					//&& minLowRecent < maxHigh
					// check width of fork
					(maxHigh - minLowRecent) >= FORK_WIDTH //2*atr
					// dont draw down trend forks
					&& !downTrend) {
					
						changeRecentLow = true;
						downTrend = false;
						
				} else {
					changeRecentLow = false;
				}
				
				double mlSlope = -1;
				double mid = -1;
				double dY = -1;
				double dX = -1;
				delta = maxHighBarsAgo - minLowRecentBarsAgo;
				
				if(changeHigh && changeMajorLow && changeRecentLow) {	
					mid = (maxHigh + minLowRecent)/2;
					medianBarsAgo = (maxHighBarsAgo + minLowRecentBarsAgo)/2;
					medianHBarsAgo = (maxHighBarsAgo + medianBarsAgo)/2;
					medianLBarsAgo = (medianBarsAgo + minLowRecentBarsAgo)/2;
					dY = mid - minLow;
					dX = Math.Abs(medianBarsAgo - minLowBarsAgo);
					mlSlope = dY/dX;
					drawFork = false;
					// check pitchfork slope
					//double bc = maxHigh - minLowRecent;
					//if( bc > 2*atr && mlSlope > 0
					// make sure high is between two lows
					if(mlSlope > 0 
						&& minLowBarsAgo > maxHighBarsAgo && minLowRecentBarsAgo < maxHighBarsAgo)
					{				
						drawFork = true;
					}
					if(barCounter >= 1 && barCounter < FORK_LENGTH) {	
						if(minLowRecent > lmLine[barCounter]) {
					   		drawFork = false;						
						} else {
							modifiedSchiff = false;
						}
					}
				}
				
				if (drawFork) {	
					// mediansRecordedBA is just the [0] reference for the median lines we compute
					// so in the future bars, we just need CurrentBar - medianRecordedBA to obtain current index into median lines
					mediansRecordedBarsAgo = CurrentBar;
					// if slope is greater than 0.75, we use modified schiff
					if(mlSlope > SLOPE_LIMIT_MSFK && (maxHigh - minLow) < 2*FORK_WIDTH) {
	                    //minLowBarsAgo += delta;
						maxHighBarsAgo += delta;
						minLowRecentBarsAgo += delta;
						mid = minLowRecent;
						minLowRecent = minLowRecent - (maxHigh - minLowRecent);
						medianBarsAgo = minLowRecentBarsAgo;
						minLowRecentBarsAgo = medianBarsAgo - delta;
						medianHBarsAgo = (maxHighBarsAgo + medianBarsAgo)/2;
						medianLBarsAgo = (medianBarsAgo + minLowRecentBarsAgo)/2;
						dY = mid - minLow;
						dX = Math.Abs(medianBarsAgo - minLowBarsAgo);
						mlSlope = dY/dX;
						if(mlSlope < 0 ) {
							drawFork = false;
							modifiedSchiff = false;
							delta = 0;
						} else {
							modifiedSchiff = true;
							Draw.Dot(this, "schiff"+dotCounter, true, medianBarsAgo-delta, Math.Round(mid), Brushes.Yellow);
							dotCounter++;
						}
					} else if(mlSlope >=2 && (maxHigh - minLow) >= 2*FORK_WIDTH) {
						modifiedSchiff = false;
						// compute only the bcParallel
						//################# BC is the sliding parallel ###########################
						dY = minLowRecent - minLow;
						dX = Math.Abs(minLowRecentBarsAgo - minLowBarsAgo);
						mlSlope = dY/dX;
						lmLine[0] = minLowRecent;
						forkSlope = (float) mlSlope;
						forkWidth = hmLine[0] - lmLine[0];
						for(int i=1; i<FORK_LENGTH; i++) {
							lmLine[i] = lmLine[0] + mlSlope*(i);
						}
						barCounter = 0;
						if(DRAW_TRIANGLES) {
		                    Draw.Line(this, "ab"+counter, minLowBarsAgo, minLow, maxHighBarsAgo, maxHigh, Brushes.Blue);
							Draw.Line(this, "bc"+counter, maxHighBarsAgo, maxHigh, minLowRecentBarsAgo, minLowRecent, Brushes.Red);
							Draw.Line(this, "ac"+counter, minLowBarsAgo, minLow, minLowRecentBarsAgo, minLowRecent, Brushes.Black);
						}
						forkId = "bc:"+Math.Round(mlSlope, 2)+"-"+Math.Round(atr, 2);
						forkWidth = maxHigh - lmLine[0];
						Draw.Text(this, "id"+counter, forkId, 
							(int) (minLowBarsAgo+minLowRecentBarsAgo)/2, (minLow+minLowRecent)/2, Brushes.Black);
						counter++;
						drawFork = false;
						if(entryOrder != null)
						{
							Draw.ArrowDown(this, "o"+dotCounter, true, 0, Close[0], Brushes.Red);
							Draw.Text(this, "cl"+dotCounter, ""+entryOrder.Name, 0, Low[0], Brushes.Red);
	                        dotCounter++;
							CancelOrder(entryOrder);
							entryOrder = null;						
						}
						changeHigh = changeMajorLow = changeRecentLow = false;
					}
					
					if(drawFork) {
						mLine[0] = mid;
						hmLine[0] = maxHigh;
						mhmLine[0] = (maxHigh + mid)/2;
						lmLine[0] = minLowRecent;
						mlmLine[0] = (minLowRecent + mid)/2;
						forkSlope = (float) mlSlope;
						
						for(int i=0; i<FORK_LENGTH; i++) {
							mLine[i] = mLine[0] + mlSlope*(i + medianBarsAgo);// + (int)2*delta/2);
						}
						
						for(int i=0; i<FORK_LENGTH; i++) {
							mhmLine[i] = mhmLine[0] + mlSlope*(i + medianHBarsAgo);// + (int) 3*delta/2);
						}
										
						for(int i=0; i<FORK_LENGTH; i++) {
							hmLine[i] = hmLine[0] + mlSlope*(i + maxHighBarsAgo);// + (int) 4*delta/2);
						}
						
						for(int i=1; i<FORK_LENGTH; i++) {
							lmLine[i] = lmLine[0] + mlSlope*(i);
						}
						
						for(int i=0; i<FORK_LENGTH; i++) {
							mlmLine[i] = mlmLine[0] + mlSlope*(i + medianLBarsAgo);// + (int) delta/2);
						}
						//Draw.Dot(this, "dh"+dotCounter, true, (int) maxHighBarsAgo, maxHigh, Brushes.Green);
	                    //Draw.Dot(this, "dml"+dotCounter, true, (int)minLowBarsAgo, minLow, Brushes.Red);
	                    //Draw.Dot(this, "dmlr"+dotCounter, true, (int)minLowRecentBarsAgo, minLowRecent-atr, Brushes.Pink);
	                    //dotCounter++;
						if(!modifiedSchiff) {
							barCounter = 0;
							forkId = "fk:"+Math.Round(mlSlope, 2)+"-"+Math.Round(atr, 2);
							forkWidth = hmLine[0] - lmLine[0];
						} else {
							barCounter = -1*delta;
	                    	forkId = "msfk:"+Math.Round(mlSlope, 2)+"-"+Math.Round(atr, 2);
							forkWidth = hmLine[0] - lmLine[0];
						}
	                    // just draw triangles
						if(!modifiedSchiff && DRAW_TRIANGLES) {
							barCounter = 0;
	                        Draw.Line(this, "ab"+counter, minLowBarsAgo, minLow, maxHighBarsAgo, maxHigh, Brushes.Blue);
							Draw.Line(this, "bc"+counter, maxHighBarsAgo, maxHigh, minLowRecentBarsAgo, minLowRecent, Brushes.Red);
							Draw.Line(this, "ac"+counter, minLowBarsAgo, minLow, minLowRecentBarsAgo, minLowRecent, Brushes.Black);
							Draw.Text(this, "id"+counter, forkId, 
								(int) (minLowBarsAgo+minLowRecentBarsAgo)/2, (minLow+minLowRecent)/2, Brushes.MediumSeaGreen);
						} else if(modifiedSchiff && DRAW_TRIANGLES) {
							Draw.Line(this, "ab"+counter, minLowBarsAgo, minLow, maxHighBarsAgo-delta, maxHigh, Brushes.Blue);
							Draw.Line(this, "bc"+counter, maxHighBarsAgo-delta, maxHigh, minLowRecentBarsAgo-delta, minLowRecent, Brushes.Red);
							Draw.Line(this, "ac"+counter, minLowBarsAgo, minLow, minLowRecentBarsAgo-delta, minLowRecent, Brushes.Black);
							Draw.Text(this, "id"+counter, forkId, 
								(int) (minLowBarsAgo+minLowRecentBarsAgo)/2, (minLow+minLowRecent)/2, Brushes.YellowGreen);
						}
						//Draw.Text(this, "slp"+counter, ""+mlSlope, (int)maxHighBarsAgo, maxHigh+2);
						counter++;
						// if we drew a new fork and have a current position, exit pls
						//if (Position.MarketPosition == MarketPosition.Long) {
						//	ExitLong("touch");
						//	entryOrder = null;
						//}
						// if we have an as yet unfilled limit order from a previous fork, cancel it
						if(entryOrder != null && drawFork) //(CurrentBar - barNumberOfOrder) >= (int) atr)) 
						{
							Draw.ArrowDown(this, "o"+dotCounter, true, 0, Close[0], Brushes.Red);
							Draw.Text(this, "cl"+dotCounter, ""+entryOrder.Name, 0, Low[0], Brushes.Red);
	                        dotCounter++;
							CancelOrder(entryOrder);
							entryOrder = null;						
						}					
						//
						drawFork = false;
						changeHigh = changeMajorLow = changeRecentLow = false;
					}
	  			}
				//if(barCounter!=-99) {
				//	Draw.Text(this, "brc"+dotCounter, ""+barCounter, 
				//		0, Low[0]-1, Brushes.Coral);
				//	dotCounter++;
				//}
					
	            // ###################################################################################################
	            // ########################### check for entries #####################################################
				if (forkId.IndexOf("fk") != -1 
					&& barCounter >= 1 && barCounter < FORK_LENGTH - 1) {
	                // ENTRY pitchfork lmLine barrier
	                if (Low[0] <= lmLine[barCounter]
	                    && Close[0] > lmLine[barCounter] + TickSize
						&& High[1] > lmLine[barCounter] 
						&& High[2] > lmLine[barCounter]
	                    ) {
	                    if (entryOrder == null && Position.MarketPosition == MarketPosition.Flat) {
							barNumberOfOrder = CurrentBar;
	                        Draw.ArrowUp(this, "o" + dotCounter, true, 0, lmLine[barCounter], Brushes.Green);
							Draw.Text(this, "fk"+dotCounter, " lml", 1, lmLine[barCounter], Brushes.Olive);
	                        dotCounter++;
	                        if(!atMarket)
								EnterLongMIT(0, true, 1, lmLine[barCounter]+TickSize, forkId);
							else 
								EnterLong(1, forkId);
							zeroLine = lmLine[0];
	                    }
	                }
	            }
	            if(forkId.IndexOf("msfk") != -1 
					&& barCounter >= 1 && barCounter < FORK_LENGTH - 1) { 
					// ENTRY modified schiff lmLine barrier
					if(Low[0] < lmLine[barCounter]
						&& Close[0] > lmLine[barCounter] + TickSize
						&& High[1] > lmLine[barCounter] 
						&& High[2] > lmLine[barCounter]
						) {
						if(entryOrder == null && Position.MarketPosition == MarketPosition.Flat) {
							barNumberOfOrder = CurrentBar;
							Draw.ArrowUp(this, "o"+dotCounter, true, 0, lmLine[barCounter], Brushes.Yellow);
							Draw.Text(this, "fk"+dotCounter, " msfk", 1, lmLine[barCounter], Brushes.Olive);
							//Draw.Text(this, "bc"+dotCounter, ""+Math.Round(lmLine[barCounter], 2), 0, lmLine[barCounter], Brushes.Olive);
							if(!atMarket)
								EnterLongMIT(0, true, 1, lmLine[barCounter], forkId);
							else
								EnterLong(1, forkId);
							zeroLine = lmLine[0];
						}
					}
				}
				/*
				if(forkId.IndexOf("bc") != -1
					&& barCounter >= 0 && barCounter < FORK_LENGTH - 1) {
					// ENTRY bc line barrier
					if(Low[0] < lmLine[barCounter] 
						&& Close[0] > lmLine[barCounter]
						) {
						if (entryOrder == null && Position.MarketPosition == MarketPosition.Flat) {
	                        barNumberOfOrder = CurrentBar;
	                        Draw.ArrowUp(this, "o"+dotCounter, true, 0, lmLine[barCounter], Brushes.Olive);
							Draw.Text(this, "fk"+dotCounter, " bc", 1, lmLine[barCounter], Brushes.Olive);
							//Draw.Text(this, "bc"+dotCounter, ""+Math.Round(lmLine[barCounter], 2), 0, lmLine[barCounter], Brushes.Olive);
	                        dotCounter++;
	                        EnterLongMIT(0, true, 1, lmLine[barCounter], forkId);
							zeroLine = lmLine[0];
	                    }
					}
				}*/
				if(forkId.IndexOf("fk") != -1 && forkWidth >= FORK_WIDTH
					&& barCounter >= 1 && barCounter < FORK_LENGTH - 1) {
					//ENTRY mlmLine barrier
					if(Low[0] <= mlmLine[barCounter] && Low[0] > lmLine[barCounter]
						&& High[0] <= mhmLine[barCounter]
						&& Close[0] > mlmLine[barCounter] + TickSize
						&& High[1] > mlmLine[barCounter] 
						&& High[2] > mlmLine[barCounter]
						) {
						if (entryOrder == null && Position.MarketPosition == MarketPosition.Flat) {
	                        barNumberOfOrder = CurrentBar;
	                        Draw.ArrowUp(this, "o"+dotCounter, true, 0, mlmLine[barCounter], Brushes.Olive);
							Draw.Text(this, "fk"+dotCounter, " mlm", 1, mlmLine[barCounter], Brushes.Olive);
							//Draw.Text(this, "bc"+dotCounter, "long: "+Math.Round(mlmLine[barCounter], 2), 0, mlmLine[barCounter], Brushes.Olive);
	                        if(!atMarket)
								EnterLongMIT(0, true, 1, mlmLine[barCounter], forkId);
							else
								EnterLong(1, forkId);
							zeroLine = lmLine[0];
	                    }
					}
				}
				// lower breakouts
				if( (forkId.IndexOf("fk") != -1 || forkId.IndexOf("msfk") != -1)
					&& barCounter >= 2 && barCounter < FORK_LENGTH - 1) {
					//ENTRY breakout at lmLine
					if(High[2] <= lmLine[barCounter]
						&& High[1] <= lmLine[barCounter]
						&& High[0] > lmLine[barCounter]
						&& Close[0] < lmLine[barCounter]
						) {
						if (entryOrder == null && Position.MarketPosition == MarketPosition.Flat) {
	                        barNumberOfOrder = CurrentBar;
	                        Draw.ArrowUp(this, "o"+dotCounter, true, 0, lmLine[barCounter], Brushes.Olive);
							Draw.Text(this, "k"+dotCounter, " brko", 1, High[0], Brushes.Olive);
							//Draw.Text(this, "bc"+dotCounter, ""+Math.Round(lmLine[barCounter], 2), 0, lmLine[barCounter], Brushes.Olive);
							if(!atMarket)
								EnterLongStopMarket(0, true, 1, lmLine[barCounter] + TickSize, forkId);
							else
								EnterLong(1, forkId);
							zeroLine = lmLine[0];
	                    }
					}
				}

				// exit positions that dont match this fork
				if (Position.MarketPosition == MarketPosition.Long) {
					// cancel pending unfilled orders
					if(entryOrder!=null) {
						if(entryOrder.Name != forkId) {
							Draw.ArrowDown(this, "o"+dotCounter, true, 0, Close[0], Brushes.Red);
							Draw.Text(this, "cl"+dotCounter, ""+entryOrder.Name, 0, Low[0], Brushes.Red);
							CancelOrder(entryOrder);
							entryOrder = null;		
						}					
					}
					// exit filled orders
					if(stopOrder!=null) {
						if(stopOrder.FromEntrySignal != forkId) {
							//SetTrailStop(stopOrder.FromEntrySignal, CalculationMode.Price, Position.AveragePrice, false);
							ExitLong("difk", stopOrder.FromEntrySignal);
						}
					}
				}
				// breakeven stop if 20 ticks in profit
				//############# short #################
				if (breakEvenStop && Position.MarketPosition == MarketPosition.Long 
					&& Close[0] >= Position.AveragePrice - (40 * (TickSize / 2)))
				{
					// Checks to see if our Stop Order has been submitted already
					if (stopOrder != null && stopOrder.StopPrice < Position.AveragePrice)
					{
						//SetTrailStop(CalculationMode.Ticks, 20);
						// Modifies stop-loss to breakeven
						stopOrder = ExitLongStopMarket(
							0, true, stopOrder.Quantity, 
							Position.AveragePrice + TickSize, "chkn", forkOrder.Name);
					}
				}
				
				dotCounter++;
				// only draw N pitchforks at any given time
				if(counter >= LIMIT) counter = 0;
				if(dotCounter >= DOTLIMIT) dotCounter = 0;
			} catch(Exception ex) {
				Print(ex.StackTrace);
			}
		}
		protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
		{
			if (entryOrder == null && order.Name == forkId)
    		{	
        		entryOrder = order;
				forkOrder = order;

                // Reset the entryOrder object to null if order was cancelled without any fill
                if (order.OrderState == OrderState.Cancelled && order.Filled == 0)
					entryOrder = null;
			}
		}

		protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
		{
			try {
				double atrLocal = Math.Round(ATR(WIDTH)[0]);
				double atrDollarValue =  atrLocal * Bars.Instrument.MasterInstrument.PointValue;
				// ##########################################################################
				/* We advise monitoring OnExecution to trigger submission of stop/target orders instead of OnOrderUpdate() since OnExecution() is called after OnOrderUpdate()
				which ensures your strategy has received the execution which is used for internal signal tracking. */
				if (entryOrder != null && entryOrder == execution.Order && barCounter >=0 )
				{
					if (execution.Order.OrderState == OrderState.Filled 
						|| execution.Order.OrderState == OrderState.PartFilled 
						|| (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0))
					{
						if(marketPosition == MarketPosition.Short) {
							stopOrder = ExitShortStopMarket(
								0, true, execution.Order.Filled, 
								execution.Order.AverageFillPrice - (STOP_VALUE/Bars.Instrument.MasterInstrument.PointValue), "atrShStp", forkOrder.Name);
							targetOrder = ExitShortLimit(
									0, true, execution.Order.Filled,
									Position.AveragePrice - atrDollarValue,// 48/4,
									"shortx", forkOrder.Name);
						} else {
							// adjust stop based on fill price
							// tight stop
							int myStop = STOP_VALUE;
							double pointTicks = 1/TickSize; // 4 for ES
							if(useATRStops) {
								double gap = (execution.Order.AverageFillPrice - zeroLine);
							
								gap = gap * Bars.Instrument.MasterInstrument.PointValue; // times 50 for ES
								if (gap < STOP_VALUE && atrDollarValue < STOP_VALUE) {
									// gap + 1 point stop
									myStop = (int) (gap + Bars.Instrument.MasterInstrument.PointValue); 
									// if stop less than atr*pointvalue, then atr stop
									myStop = myStop < atrDollarValue ? (int) atrDollarValue : myStop;
								} else {
									// no change to STOP setting
									myStop = STOP_VALUE;
								}
							}
							Draw.Text(this, "stp"+dotCounter, "-$"+ myStop, 0, High[0]+2*TickSize);
							Draw.Text(this, "tgt"+dotCounter, 
								""+ Math.Min(Position.AveragePrice + myStop/Bars.Instrument.MasterInstrument.PointValue, 
								Math.Round(mhmLine[barCounter])), 0, High[0]);
							dotCounter++;
							
							stopOrder = ExitLongStopMarket(
								0, true, execution.Order.Filled, 
								execution.Order.AverageFillPrice - (myStop/Bars.Instrument.MasterInstrument.PointValue), "atrStop", forkOrder.Name);
							
							// tbd instead of 60, use dollar profit targets
							if(forkOrder.Name.IndexOf("msfk") != -1) {
								targetOrder = ExitLongLimit(
									0, true, execution.Order.Filled,
									//Math.Max(Position.AveragePrice +  forkWidth/2, Position.AveragePrice + atrLocal),
									//Math.Min(Position.AveragePrice +  48/pointTicks, Math.Round(hmLine[barCounter])),
									//Math.Round(hmLine[barCounter]) -2*pointTicks,
									Math.Min(Position.AveragePrice + PROFIT_TARGET/Bars.Instrument.MasterInstrument.PointValue, 
									Math.Round(mLine[barCounter])),
									"msfk", forkOrder.Name);
							}
							if(forkOrder.Name.IndexOf("fk") != -1) {
								targetOrder = ExitLongLimit(
									0, true, execution.Order.Filled,
									//Math.Max(Position.AveragePrice +  forkWidth/2, Position.AveragePrice + atrLocal),
									//Math.Round(hmLine[barCounter]) -2*pointTicks,
									Math.Min(Position.AveragePrice + PROFIT_TARGET/Bars.Instrument.MasterInstrument.PointValue, 
									Math.Round(mLine[barCounter])),
									"fk", forkOrder.Name);
							}
							if(forkOrder.Name.IndexOf("bc") != -1) {
								targetOrder = ExitLongLimit(
									0, true, execution.Order.Filled,
									Position.AveragePrice +  PROFIT_TARGET/Bars.Instrument.MasterInstrument.PointValue,
									//Math.Max(Position.AveragePrice +  forkWidth/2, Position.AveragePrice + atrLocal), //3*myStop/Bars.Instrument.MasterInstrument.PointValue, 
									"bc", forkOrder.Name);
							}
						}

						// Resets the entryOrder object to null after the order has been filled
						if (execution.Order.OrderState != OrderState.PartFilled)
							entryOrder = null;
					}
				}

				// Reset our stop order and target orders' Order objects after our position is closed.
				if ((stopOrder != null && stopOrder == execution.Order) || (targetOrder != null && targetOrder == execution.Order))
				{
					if (execution.Order.OrderState == OrderState.Filled || execution.Order.OrderState == OrderState.PartFilled)
					{
						stopOrder = null;
						targetOrder = null;
					}
				}
			} catch(Exception ex) {
				Print(ex.StackTrace);
			}
		}
		protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
		{
			// Print our current position to the lower right hand corner of the chart
			//Draw.TextFixed(this, "MyTag", ""+position., TextPosition.BottomRight, false, "");
		}
				#region Properties
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "LIMIT", GroupName = "NinjaScriptParameters", Order = 0)]
		public int LIMIT 
		{ get; set; }
		
		[Range(1, 30), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "WIDTH", GroupName = "NinjaScriptParameters", Order = 1)]
		public int WIDTH 
		{ get; set; }
		
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "DOTLIMIT", GroupName = "NinjaScriptParameters", Order = 2)]
		public int DOTLIMIT 
		{ get; set; }
		
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "FORK_LENGTH", GroupName = "NinjaScriptParameters", Order = 3)]
		public int FORK_LENGTH 			
		{ get; set; }
		
		[Range(0, float.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "FORK_WIDTH", GroupName = "NinjaScriptParameters", Order = 4)]
		public float FORK_WIDTH	
		{ get; set; }
		
		[Range(1, 300), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "HIGH_STRENGTH", GroupName = "NinjaScriptParameters", Order = 5)]
		public int HIGH_STRENGTH
		{ get; set; }
		
		[Range(100, 3000), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "STOP_VALUE", GroupName = "NinjaScriptParameters", Order = 6)]
		public int STOP_VALUE
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "DRAW_TRIANGLES", GroupName = "NinjaScriptParameters", Order = 7)]
		public bool DRAW_TRIANGLES
		{ get; set; }
		
		[Range(1, 10), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "SLOPE_LIMIT_MSFK", GroupName = "NinjaScriptParameters", Order = 8)]
		public int SLOPE_LIMIT_MSFK
		{ get; set; }
		
		[Range(50, 3000), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "PROFIT_TARGET", GroupName = "NinjaScriptParameters", Order = 9)]
		public int PROFIT_TARGET
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "DRAW_DOTS", GroupName = "NinjaScriptParameters", Order = 10)]
		public bool drawDots
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "AT_MARKET", GroupName = "NinjaScriptParameters", Order = 11)]
		public bool atMarket
		{ get; set; }
	
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "USE_ATR_STOPS", GroupName = "NinjaScriptParameters", Order = 12)]
		public bool useATRStops
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "BREAKEVEN_STOP", GroupName = "NinjaScriptParameters", Order = 13)]
		public bool breakEvenStop
		{ get; set; }
		
		#endregion
	}
}
// but only if the price has not failed to breach the ML already
					// highest close since mediansRecordedBarsAgo is between ML and HML, no entry!	
					/*for(int i=barCounter; i<1; i--) {
						if(mLine[i] < High[barCounter-i]) {
							noEntry = true;
							break;
						}
					}*/

/*
// check if this low works for other majorHighs and minLows
				// get lowest low other than this minLowRecent
				int tempMinLowBarsAgo = 2;
				double tempMinLow = Low[2];
				for(int i=3; i<maxHighBarsAgo - 1; i++) { // only since the recent maxHigh please.
					if(tempMinLow > Low[i]) {
							tempMinLow = Low[i];
						tempMinLowBarsAgo = i;
					}
				}				
				// get high since this tempMinLow
				double tempMaxHigh = High[HighestBar(High, tempMinLowBarsAgo - 1)]; 
				int tempMaxHighBarsAgo = -1;
				if(tempMaxHigh == High[1]) {
					tempMaxHighBarsAgo = 1;
				}
				// lets find how many bars ago
				for(int i=2; i<tempMinLowBarsAgo - 1; i++) {
					if(tempMaxHigh == High[i]) {
						tempMaxHighBarsAgo = i;
					}
				}
				// check if a fork is possible with adjusted points A and B.
				if(tempMaxHighBarsAgo != -1) {
					if(// check width of fork
						(tempMaxHigh - minLowRecent) >= 8 //2*atr
						// dont draw down trend forks
						&& Math.Abs(minLowRecent - tempMinLow) <= (tempMaxHigh - tempMinLow)) {
							
						changeRecentLow = true;	
							
					}
				}*/