﻿namespace QuantConnect.BybitBrokerage.Models.Enums;

/// <summary>
/// Order Status
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Created
    /// </summary>
    Created,

    /// <summary>
    /// New
    /// </summary>
    New,

    /// <summary>
    /// Rejected
    /// </summary>
    Rejected,

    /// <summary>
    /// Partially filled
    /// </summary>
    PartiallyFilled,

    /// <summary>
    /// Partially filled canceled
    /// </summary>
    PartiallyFilledCanceled,

    /// <summary>
    /// Filled
    /// </summary>
    Filled,

    /// <summary>
    /// Cancelled
    /// </summary>
    Cancelled,

    /// <summary>
    /// Untriggered
    /// </summary>
    Untriggered,

    /// <summary>
    /// Triggered
    /// </summary>
    Triggered,

    /// <summary>
    /// Deactivated
    /// </summary>
    Deactivated,

    /// <summary>
    /// Active
    /// </summary>
    Active
}