using System.Collections.Concurrent;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Implements circuit breaker pattern to prevent retrying tasks when a provider is experiencing widespread failures.
    /// Tracks failure rates per provider and temporarily pauses retries when threshold is exceeded.
    /// </summary>
    public class ProviderCircuitBreaker
    {
        /// <summary>
        /// Circuit state for a provider
        /// </summary>
        public enum CircuitState
        {
            /// <summary>
            /// Circuit is functioning normally, requests allowed
            /// </summary>
            Closed,
            
            /// <summary>
            /// Circuit is open due to too many failures, requests blocked
            /// </summary>
            Open,
            
            /// <summary>
            /// Circuit is testing if provider has recovered, limited requests allowed
            /// </summary>
            HalfOpen
        }

        /// <summary>
        /// Tracks circuit state for a provider
        /// </summary>
        private class ProviderCircuit
        {
            public CircuitState State { get; set; } = CircuitState.Closed;
            public int ConsecutiveFailures { get; set; } = 0;
            public DateTime? OpenedAt { get; set; }
            public DateTime? LastFailureAt { get; set; }
        }

        private readonly ConcurrentDictionary<string, ProviderCircuit> _circuits = new();
        private readonly int _failureThreshold;
        private readonly TimeSpan _openDuration;
        private readonly TimeSpan _resetAfterSuccess;

        /// <summary>
        /// Creates a new circuit breaker
        /// </summary>
        /// <param name="failureThreshold">Number of consecutive failures before opening circuit (default: 3)</param>
        /// <param name="openDuration">How long to keep circuit open (default: 10 minutes)</param>
        /// <param name="resetAfterSuccess">How long without failures before resetting counter (default: 5 minutes)</param>
        public ProviderCircuitBreaker(
            int failureThreshold = 3,
            TimeSpan? openDuration = null,
            TimeSpan? resetAfterSuccess = null)
        {
            _failureThreshold = failureThreshold;
            _openDuration = openDuration ?? TimeSpan.FromMinutes(10);
            _resetAfterSuccess = resetAfterSuccess ?? TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Records a failure for a provider
        /// </summary>
        /// <param name="provider">Provider name (e.g., "openai", "claude")</param>
        public void RecordFailure(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return;
            }

            var circuit = _circuits.GetOrAdd(provider.ToLowerInvariant(), _ => new ProviderCircuit());

            lock (circuit)
            {
                circuit.ConsecutiveFailures++;
                circuit.LastFailureAt = DateTime.UtcNow;

                // Open circuit if threshold exceeded
                if (circuit.State == CircuitState.Closed && 
                    circuit.ConsecutiveFailures >= _failureThreshold)
                {
                    circuit.State = CircuitState.Open;
                    circuit.OpenedAt = DateTime.UtcNow;
                }
                else if (circuit.State == CircuitState.HalfOpen)
                {
                    // Failed during test, reopen circuit
                    circuit.State = CircuitState.Open;
                    circuit.OpenedAt = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Records a success for a provider
        /// </summary>
        /// <param name="provider">Provider name</param>
        public void RecordSuccess(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return;
            }

            var circuit = _circuits.GetOrAdd(provider.ToLowerInvariant(), _ => new ProviderCircuit());

            lock (circuit)
            {
                // Reset failure counter and close circuit
                circuit.ConsecutiveFailures = 0;
                circuit.State = CircuitState.Closed;
                circuit.OpenedAt = null;
            }
        }

        /// <summary>
        /// Checks if retries are allowed for a provider
        /// </summary>
        /// <param name="provider">Provider name</param>
        /// <returns>True if retries are allowed, false if circuit is open</returns>
        public bool CanRetry(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return true; // Allow if no provider specified
            }

            var circuit = _circuits.GetOrAdd(provider.ToLowerInvariant(), _ => new ProviderCircuit());

            lock (circuit)
            {
                var now = DateTime.UtcNow;

                // Check if we should transition to half-open
                if (circuit.State == CircuitState.Open && 
                    circuit.OpenedAt.HasValue &&
                    now - circuit.OpenedAt.Value >= _openDuration)
                {
                    circuit.State = CircuitState.HalfOpen;
                }

                // Check if we should reset failure counter due to inactivity
                if (circuit.State == CircuitState.Closed &&
                    circuit.LastFailureAt.HasValue &&
                    now - circuit.LastFailureAt.Value >= _resetAfterSuccess)
                {
                    circuit.ConsecutiveFailures = 0;
                }

                // Allow retries if closed or half-open
                return circuit.State != CircuitState.Open;
            }
        }

        /// <summary>
        /// Gets the current state of a provider's circuit
        /// </summary>
        /// <param name="provider">Provider name</param>
        /// <returns>Current circuit state</returns>
        public CircuitState GetState(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return CircuitState.Closed;
            }

            if (_circuits.TryGetValue(provider.ToLowerInvariant(), out var circuit))
            {
                lock (circuit)
                {
                    return circuit.State;
                }
            }

            return CircuitState.Closed;
        }

        /// <summary>
        /// Gets diagnostic information about all circuits
        /// </summary>
        /// <returns>Dictionary of provider states</returns>
        public Dictionary<string, (CircuitState State, int Failures, DateTime? LastFailure)> GetAllStates()
        {
            var result = new Dictionary<string, (CircuitState, int, DateTime?)>();

            foreach (var kvp in _circuits)
            {
                lock (kvp.Value)
                {
                    result[kvp.Key] = (
                        kvp.Value.State,
                        kvp.Value.ConsecutiveFailures,
                        kvp.Value.LastFailureAt
                    );
                }
            }

            return result;
        }

        /// <summary>
        /// Manually resets a provider's circuit (for testing or manual intervention)
        /// </summary>
        /// <param name="provider">Provider name</param>
        public void Reset(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return;
            }

            if (_circuits.TryGetValue(provider.ToLowerInvariant(), out var circuit))
            {
                lock (circuit)
                {
                    circuit.State = CircuitState.Closed;
                    circuit.ConsecutiveFailures = 0;
                    circuit.OpenedAt = null;
                    circuit.LastFailureAt = null;
                }
            }
        }

        /// <summary>
        /// Resets all circuits (for testing or full system reset)
        /// </summary>
        public void ResetAll()
        {
            _circuits.Clear();
        }
    }
}
