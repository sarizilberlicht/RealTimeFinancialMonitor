import { useState } from "react";
import type {
    Transaction,
    TransactionStatus,
} from "../types/Transaction";
import "./AddTransactionPage.css";

export default function AddTransactionPage() {
    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL;
    const [amount, setAmount] = useState("");
    const [currency, setCurrency] = useState("USD");
    const [status, setStatus] =
        useState<TransactionStatus>("Pending");

    const [error, setError] = useState("");
    const [message, setMessage] = useState("");

    const sendTransaction = async () => {
        const numericAmount = Number(amount);

        if (!amount || numericAmount <= 0) {
            setError("Amount must be greater than zero.");
            setMessage("");
            return;
        }

        setError("");
        setMessage("");

        const transaction: Transaction = {
            transactionId: crypto.randomUUID(),
            amount: numericAmount,
            currency,
            status,
            timestamp: new Date().toISOString(),
        };

        try {
            const response = await fetch(
                `${apiBaseUrl}/api/transactions`,
                {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                    },
                    body: JSON.stringify(transaction),
                }
            );

            if (!response.ok) {
                throw new Error("Failed to send transaction.");
            }

            setMessage("Transaction sent successfully.");
            setAmount("");
        } catch {
            setError("Failed to send transaction.");
            setMessage("");
        }
    };

    return (
        <section className="add-page">
            <div className="transaction-form">
                <div>
                    <h2>Transaction Simulator</h2>
                    <p>
                        Create a transaction and send it to the backend.
                    </p>
                </div>

                <label className="form-field">
                    <span>Amount</span>

                    <input
                        type="number"
                        value={amount}
                        onChange={event =>
                            setAmount(event.target.value)
                        }
                        placeholder="Enter amount"
                    />
                </label>

                <label className="form-field">
                    <span>Currency</span>

                    <select
                        value={currency}
                        onChange={event =>
                            setCurrency(event.target.value)
                        }
                    >
                        <option value="USD">USD</option>
                        <option value="EUR">EUR</option>
                        <option value="ILS">ILS</option>
                    </select>
                </label>

                <label className="form-field">
                    <span>Status</span>

                    <select
                        value={status}
                        onChange={event =>
                            setStatus(
                                event.target.value as TransactionStatus
                            )
                        }
                    >
                        <option value="Pending">Pending</option>
                        <option value="Completed">Completed</option>
                        <option value="Failed">Failed</option>
                    </select>
                </label>

                {error && (
                    <p className="form-error" role="alert">
                        {error}
                    </p>
                )}

                {message && (
                    <p className="form-success" role="status">
                        {message}
                    </p>
                )}

                <button
                    className="primary-button"
                    onClick={sendTransaction}
                >
                    Send Transaction
                </button>
            </div>
        </section>
    );
}