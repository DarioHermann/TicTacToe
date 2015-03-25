﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using System.Threading.Tasks;
using TicTacToe.Server.Models;

namespace TicTacToe.Server
{
    public class GameHub : Hub
    {
        /// <summary>
        /// The starting point for a client looking to join a new game.
        /// Player either starts a game with a waiting opponent or joins the waiting pool.
        /// </summary>
        /// <param name="username">The friendly name that the user has chosen.</param>
        /// <returns>A Task to track the asynchronous method execution.</returns>
        public async Task FindGame(string username)
        {
            Player joiningPlayer = 
                GameState.Instance.CreatePlayer(username, this.Context.ConnectionId);
            this.Clients.Caller.playerJoined(joiningPlayer);
            
            // Find any pending games if any
            Player opponent = GameState.Instance.GetWaitingOpponent();
            if (opponent == null)
            {
                // No waiting players so enter the waiting pool
                GameState.Instance.AddToWaitingPool(joiningPlayer);
                this.Clients.Caller.waitingList();
            }
            else
            {
                // An opponent was found so join a new game and start the game
                // Opponent is first player since they were waiting first
                Game newGame = await GameState.Instance.CreateGame(opponent, joiningPlayer);
                Clients.Group(newGame.Id).start(newGame);
            }
        }

        /// <summary>
        /// Client has requested to place a piece down in the following position.
        /// </summary>
        /// <param name="row">The row part of the position.</param>
        /// <param name="col">The column part of the position.</param>
        /// <returns>A Task to track the asynchronous method execution.<</returns>
        public async Task PlacePiece(int row, int col)
        {
            Player playerMakingTurn = GameState.Instance.GetPlayer(playerId: this.Context.ConnectionId);
            Player opponent;
            Game game = GameState.Instance.GetGame(playerMakingTurn, out opponent);

            // TODO: should the game check if it is the players turn?
            if (game == null || game.WhoseTurn != playerMakingTurn.Id)
            {
                this.Clients.Caller.notPlayersTurn();
                return;
            }

            // Notify everyone of the valid move. Only send what is necessary (instead of sending whole board)
            game.PlacePiece(row, col);
            this.Clients.Group(game.Id).piecePlaced(row, col, playerMakingTurn.Piece);
            this.Clients.Group(game.Id).updateTurn(game);
            await Task.Factory.StartNew(() => { });
        }

        /// <summary>
        /// A player that is leaving should end all games and notify the opponent.
        /// </summary>
        /// <param name="stopCalled"></param>
        /// <returns></returns>
        public override async Task OnDisconnected(bool stopCalled)
        {
            Player leavingPlayer = GameState.Instance.GetPlayer(playerId: this.Context.ConnectionId);

            // Only handle cases where user was a player in a game or waiting for an opponent
            if (leavingPlayer != null)
            {
                Player opponent;
                Game ongoingGame = GameState.Instance.GetGame(leavingPlayer, out opponent);
                if (ongoingGame != null)
                {
                    this.Clients.Group(ongoingGame.Id).opponentLeft();
                    GameState.Instance.RemoveGame(ongoingGame.Id);
                }
            }
            
            await base.OnDisconnected(stopCalled);
        }
    }
}