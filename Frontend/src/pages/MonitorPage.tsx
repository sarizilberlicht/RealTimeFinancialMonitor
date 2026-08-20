import { useEffect, useState } from "react";
import * as signalR from "@microsoft/signalr";
import type { Transaction } from "../types/Transaction";
import "./MonitorPage.css";

const sortByNewest = (items: Transaction[]) => {
    return [...items].sort(
        (a, b) =>
            new Date(b.timestamp).getTime() -
            new Date(a.timestamp).getTime()
    );
};

export default function MonitorPage() {

    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL;
    const [transactions, setTransactions] = useState<Transaction[]>([]);
    type StatusFilter =
        "All" |
        "Pending" |
        "Completed" |
        "Failed";

    const [statusFilter, setStatusFilter] =
        useState<StatusFilter>("All");


    useEffect(() => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${apiBaseUrl}/transactionHub`, {
                transport: signalR.HttpTransportType.WebSockets,
                skipNegotiation: true,
            }).withAutomaticReconnect()
            .build();

        const addOrUpdateTransaction = (transaction: Transaction) => {
            setTransactions(current => {
                const alreadyExists = current.some(
                    item =>
                        item.transactionId ===
                        transaction.transactionId
                );

                if (!alreadyExists) {
                    return sortByNewest([
                        ...current,
                        transaction,
                    ]);
                }

                const updatedTransactions = current.map(
                    item =>
                        item.transactionId === transaction.transactionId
                            ? transaction
                            : item
                );

                return sortByNewest(updatedTransactions);
            });
        };

        connection.on(
            "TransactionReceived",
            addOrUpdateTransaction
        );

        const initialize = async () => {
            try {
                await connection.start();

                const response = await fetch(
                    `${apiBaseUrl}/api/transactions`
                );

                if (!response.ok) {
                    throw new Error(
                        "Failed to load transactions."
                    );
                }

                const existingTransactions: Transaction[] =
                    await response.json();

                setTransactions(current => {
                    const transactionsById = new Map(
                        current.map(transaction => [
                            transaction.transactionId,
                            transaction,
                        ])
                    );

                    for (const transaction of existingTransactions) {
                        transactionsById.set(
                            transaction.transactionId,
                            transaction
                        );
                    }

                    return sortByNewest(
                        Array.from(transactionsById.values())
                    );
                });
            } catch (error) {
                console.error(
                    "Failed to initialize monitor:",
                    error
                );
            }
        };

        initialize();

        return () => {
            connection.stop();
        };
    }, []);

    const visibleTransactions =
        statusFilter === "All"
            ? transactions
            : transactions.filter(
                transaction =>
                    transaction.status === statusFilter
            );

    const pendingCount = transactions.filter(
        transaction =>
            transaction.status === "Pending"
    ).length;

    const completedCount = transactions.filter(
        transaction =>
            transaction.status === "Completed"
    ).length;

    const failedCount = transactions.filter(
        transaction =>
            transaction.status === "Failed"
    ).length;

    return (
        <section className="monitor-page">
            <div className="page-header">
                <div>
                    <h2>Live Transaction Monitor</h2>
                    <p>
                        Transactions are updated in real time.
                    </p>
                </div>
            </div>

            <div className="filter-bar">
                <button
                    className={
                        statusFilter === "All"
                            ? "filter-button active"
                            : "filter-button"
                    }
                    onClick={() => setStatusFilter("All")}
                >
                    All ({transactions.length})
                </button>

                <button
                    className={
                        statusFilter === "Pending"
                            ? "filter-button active"
                            : "filter-button"
                    }
                    onClick={() =>
                        setStatusFilter("Pending")
                    }
                >
                    Pending ({pendingCount})
                </button>

                <button
                    className={
                        statusFilter === "Completed"
                            ? "filter-button active"
                            : "filter-button"
                    }
                    onClick={() =>
                        setStatusFilter("Completed")
                    }
                >
                    Completed ({completedCount})
                </button>

                <button
                    className={
                        statusFilter === "Failed"
                            ? "filter-button active"
                            : "filter-button"
                    }
                    onClick={() =>
                        setStatusFilter("Failed")
                    }
                >
                    Failed ({failedCount})
                </button>
            </div>

            <div className="table-container">
                <table className="transactions-table">
                    <thead>
                        <tr>
                            <th>Transaction ID</th>
                            <th>Amount</th>
                            <th>Currency</th>
                            <th>Status</th>
                            <th>Timestamp</th>
                        </tr>
                    </thead>

                    <tbody>
                        {visibleTransactions.map(transaction => (
                            <tr
                                key={transaction.transactionId}
                                className="transaction-row">
                                <td>{transaction.transactionId}</td>
                                <td>{transaction.amount}</td>
                                <td>{transaction.currency}</td>
                                <td>
                                    <span
                                        className={`status-badge status-${transaction.status.toLowerCase()}`}
                                    >
                                        {transaction.status}
                                    </span>
                                </td>
                                <td>
                                    {new Date(
                                        transaction.timestamp
                                    ).toLocaleString()}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </section>
    );
}