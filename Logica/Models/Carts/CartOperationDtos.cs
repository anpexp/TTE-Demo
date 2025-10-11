using System;
using System.Collections.Generic;

namespace Logica.Models.Carts
{
    /// <summary>
    /// DTO para resultado de sincronización de un carrito individual
    /// </summary>
    public class CartSyncResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid? LocalCartId { get; set; }
        public int FakeStoreCartId { get; set; }
        public int ProductsSynced { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<int> InvalidProductIds { get; set; } = new();
        public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
        public string Operation { get; set; } = string.Empty; // "Created", "Updated", "Skipped"
    }

    /// <summary>
    /// DTO para resultado de sincronización en lote de carritos
    /// </summary>
    public class CartSyncBatchResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalCartsProcessed { get; set; }
        public int CartsSuccessful { get; set; }
        public int CartsFailed { get; set; }
        public List<CartSyncResultDto> Results { get; set; } = new();
        public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
        
        // Additional detailed properties
        public int CartsCreated { get; set; }
        public int CartsUpdated { get; set; }
        public int CartsSkipped { get; set; }
        public List<string> DetailedErrors { get; set; } = new();
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration => CompletedAt - StartedAt;
        public bool HasErrors => CartsFailed > 0;
        public double SuccessRate => TotalCartsProcessed > 0 ? (double)CartsSuccessful / TotalCartsProcessed * 100 : 0;
    }
}