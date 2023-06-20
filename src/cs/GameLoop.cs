/**
	Sustainable Energy Development game modeling the Swiss energy Grid.
	Copyright (C) 2023 Università della Svizzera Italiana

	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using Godot;
using System;
using System.Diagnostics;
using System.Collections.Generic;

// Contains all of the fields related to money
public struct MoneyData {
	public int Money; // Total amount of available money
	public int Budget; // Total budget for this round
	public int Production; // Money spent on production costs this round
	public int Build; // Money spent on build costs this round

	// Default constructor for the MoneyData
	public MoneyData(int start_money) {
		Money = start_money;
		Budget = start_money;
		Production = 0;
		Build = 0;
	}

	// Resets the spending statistics at the end of each round
	public void NextTurn(int new_budget) {
		Money += new_budget;
		Budget = Money;
		Production = 0;
		Build = 0;
	}

	// Spends money by updating the data correctly
	public void SpendMoney(int amountBuild, int amountProd=0) {
		Build += amountBuild;
		Production += amountProd;
		Money = Budget - (Build + Production);
	}
}

// Models the overarching game loop, which controls every aspect of the game
// and makes sure that things are synchronized across game objects
public partial class GameLoop : Node2D {

	// Represents the various states that the game can be in
	public enum GameState { NOT_STARTED, PLAYING, ENDED };

	// The total number of turns in the game
	[Export]
	public int N_TURNS = 10;

	// The amount of money the player starts with (in millions of CHF)
	[Export]
	public int START_MONEY = 1000;

	[Export]
	public int BUDGET_PER_TURN = 1000;

	// Internal game state
	private GameState GS;

	// Contains all of the buildings in the scene
	private List<PowerPlant> Buildings;
	
	// Contains all of the BuildButtons in the scene
	private List<BuildButton> BBs;

	// Reference to the UI
	private UI _UI;

	// Reference to the buildmenu
	private BuildMenu BM;

	//TODO: Add resource managers once they are implemented
	private MoneyData Money; // The current money the player has

	private int RemainingTurns; // The number of turns remaining until the end of the game

	//TODO: Add Shocks once they are implemented

	// ==================== GODOT Method Overrides ====================

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		// Init Data
		GS = GameState.NOT_STARTED;
		RemainingTurns = N_TURNS;
		Money = new MoneyData(START_MONEY);
		Buildings = new List<PowerPlant>();
		BBs = new List<BuildButton>();

		// Fetch initial nodes
		// Start with buildings, in the begining there are only 2 buildings Nuclear and Coal
		Buildings.Add(GetNode<PowerPlant>("World/Nuclear"));
		Buildings.Add(GetNode<PowerPlant>("World/Coal"));

		// Fill in build buttons
		BBs.Add(GetNode<BuildButton>("World/BuildButton"));
		BBs.Add(GetNode<BuildButton>("World/BuildButton2"));
		BBs.Add(GetNode<BuildButton>("World/BuildButton3"));
		BBs.Add(GetNode<BuildButton>("World/BuildButton4"));

		// Fetch UI and BuildMenu
		_UI = GetNode<UI>("UI");
		BM = GetNode<BuildMenu>("BuildMenu");

		// Connect Callback to each build button and give them a reference to the loop
		foreach(BuildButton bb in BBs) {
			bb.UpdateBuildSlot += _OnUpdateBuildSlot;

			// Record a reference to the game loop
			bb._RecordGameLoopRef(this);
		}

		// Connect to the UI's next turn signal
		_UI.NextTurn += _OnNextTurn;

		// Start the game
		StartGame();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta) {
	}

	// ==================== Resource access API ====================
	
	// Checks if a current build is legal and if so updates the amount of money
	public bool _RequestBuild(int cost) {
		// Check that we have enough money
		if(Money.Money >= cost) {
			// Spend some money
			Money.SpendMoney(cost);

			// Notify the UI of the resource update
			UpdateResources();
			return true;
		}
		// Return false if we couldn't afford the build
		return false;
	}

	// ==================== Internal Helpers ====================
	
	// Propagates resource updates to the UI
	private void UpdateResources() {
		// Update Money UI
		_UI._UpdateData(
			UI.InfoType.MONEY,
			Money.Budget, 
			Money.Production,
			Money.Build,
			Money.Money
		);
	}

	// Propagates the new turn to each build button and building
	private void UpdateBuilds() {
		foreach(var bb in BBs) {
			bb._NextTurn();
		}
		foreach(var pp in Buildings) {
			pp._NewTurn();
		}
	}

	// ==================== Main Game Loop Methods ====================  

	// Initializes all of the data that is propagated across the game
	// This only happens once at the begining of the playthrough
	private void StartGame() {
		// Update the game state
		GS = GameState.PLAYING;

		// Perform initial Resouce update
		UpdateResources();
	}

	// Triggers all of the updates across the whole game at the beginnig of the turn
	// A new turn is triggered when the player presses the next turn button in the UI.
	private void NewTurn() {
		// Decerement the remaining turns and check for game end
		if((GS == GameState.PLAYING) && (RemainingTurns-- > 0)) {
			// TODO Regular game loop
			// Start by reseting the money data for the new turn 
			Money.NextTurn(BUDGET_PER_TURN);

			// Update Resources 
			UpdateResources();

			// Update the buildings and build buttons
			UpdateBuilds();

		} else if(RemainingTurns <= 0) {
			// End the game if all turns have been spent
			EndGame();
		}
	}

	// Triggers all of the actions that happen at the end of the game
	// The end of the game is triggered when the number of remaining turns reaches 0
	private void EndGame() {
		// Update the game state 
		GS = GameState.ENDED;
	}

	// ==================== Interaction Callbacks ====================
	
	// Updates the internal lists on every build slot update
	public void _OnUpdateBuildSlot(BuildButton bb, PowerPlant pp, bool remove) {
		// Check if the update was a power plant addition or removal
		if(remove) {
			//Sanity Check
			Debug.Assert(Buildings.Contains(pp));

			// Destroy the power plant
			Buildings.Remove(pp);

			// Connect the new build button to our signal
			bb.UpdateBuildSlot += _OnUpdateBuildSlot;

			// Make sure that it has access to the game loop
			bb._RecordGameLoopRef(this);

			// Replace it with the new build button
			BBs.Add(bb);
		} else {
			// Sanity Check
			Debug.Assert(BBs.Contains(bb));

			// Destroy the Build Button
			BBs.Remove(bb);

			// Replace it with the new power plant
			Buildings.Add(pp);
		}
	}

	// Triggers a new turn if the game is currently acitve
	public void _OnNextTurn() {
		if(GS == GameState.PLAYING) {
			NewTurn();
		}
	}
}