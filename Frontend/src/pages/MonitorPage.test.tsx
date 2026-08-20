import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import MonitorPage from "./MonitorPage";
import userEvent from "@testing-library/user-event";

let transactionReceivedHandler:
    ((transaction: {
        transactionId: string;
        amount: number;
        currency: string;
        status: "Pending" | "Completed" | "Failed";
        timestamp: string;
    }) => void) | undefined;

const withUrlMock = vi.fn().mockReturnThis();

vi.mock("@microsoft/signalr", () => {
    return {
        HttpTransportType: {
            WebSockets: 1,
        },

        HubConnectionBuilder: vi.fn(function () {
            return {
                withUrl: withUrlMock,
                withAutomaticReconnect: vi.fn().mockReturnThis(),

                build: vi.fn().mockReturnValue({
                    on: vi.fn((eventName, handler) => {
                        if (eventName === "TransactionReceived") {
                            transactionReceivedHandler = handler;
                        }
                    }),

                    start: vi.fn().mockResolvedValue(undefined),
                    stop: vi.fn().mockResolvedValue(undefined),
                }),
            };
        }),
    };
});

describe("MonitorPage", () => {
    beforeEach(() => {
        withUrlMock.mockClear();
        transactionReceivedHandler = undefined;

        vi.spyOn(globalThis, "fetch")
            .mockResolvedValue(
                new Response(
                    JSON.stringify([]),
                    {
                        status: 200,
                        headers: {
                            "Content-Type": "application/json",
                        },
                    }
                )
            );
    });
    it("should display the transactions table", () => {
        render(<MonitorPage />);

        expect(
            screen.getByRole("table")
        ).toBeInTheDocument();
    });

    it("should display a received transaction in the table", async () => {
        render(<MonitorPage />);

        const transaction = {
            transactionId: "123",
            amount: 1500.50,
            currency: "USD",
            status: "Completed" as const,
            timestamp: "2026-08-14T10:00:00Z",
        };

        transactionReceivedHandler?.(transaction);

        expect(
            await screen.findByText("USD")
        ).toBeInTheDocument();

        expect(
            screen.getByText("1500.5")
        ).toBeInTheDocument();

        expect(
            screen.getByText("Completed")
        ).toBeInTheDocument();
    });

    it("should filter transactions by status", async () => {
        render(<MonitorPage />);

        const completedTransaction = {
            transactionId: "1",
            amount: 100,
            currency: "USD",
            status: "Completed" as const,
            timestamp: "2026-08-14T10:00:00Z",
        };

        const failedTransaction = {
            transactionId: "2",
            amount: 200,
            currency: "EUR",
            status: "Failed" as const,
            timestamp: "2026-08-14T10:01:00Z",
        };

        transactionReceivedHandler?.(completedTransaction);
        transactionReceivedHandler?.(failedTransaction);

        expect(await screen.findByText("USD")).toBeInTheDocument();
        expect(screen.getByText("EUR")).toBeInTheDocument();

        await userEvent.click(
            screen.getByRole("button", {
                name: /^failed/i,
            })
        );

        expect(
            screen.queryByText("USD")
        ).not.toBeInTheDocument();

        expect(
            screen.getByText("EUR")
        ).toBeInTheDocument();
    });
    it("should display existing transactions loaded from the backend", async () => {
        vi.mocked(globalThis.fetch).mockResolvedValueOnce(
            new Response(
                JSON.stringify([
                    {
                        transactionId: "existing-1",
                        amount: 300,
                        currency: "USD",
                        status: "Completed",
                        timestamp: "2026-08-16T10:00:00Z",
                    },
                ]),
                {
                    status: 200,
                    headers: {
                        "Content-Type": "application/json",
                    },
                }
            )
        );

        render(<MonitorPage />);

        expect(
            await screen.findByText("existing-1")
        ).toBeInTheDocument();

        expect(
            screen.getByText("300")
        ).toBeInTheDocument();

        expect(
            screen.getByText("Completed")
        ).toBeInTheDocument();
    });
    it("should not display the same transaction twice when received from GET and SignalR", async () => {
        const transaction = {
            transactionId: "same-id",
            amount: 500,
            currency: "USD",
            status: "Pending" as const,
            timestamp: "2026-08-16T11:00:00Z",
        };

        vi.mocked(globalThis.fetch).mockResolvedValueOnce(
            new Response(
                JSON.stringify([transaction]),
                {
                    status: 200,
                    headers: {
                        "Content-Type": "application/json",
                    },
                }
            )
        );

        render(<MonitorPage />);

        expect(
            await screen.findByText("same-id")
        ).toBeInTheDocument();

        transactionReceivedHandler?.(transaction);

        const rows = screen.getAllByText("same-id");

        expect(rows).toHaveLength(1);
    });
    it("should display transaction status as a status indicator", async () => {
        render(<MonitorPage />);

        transactionReceivedHandler?.({
            transactionId: "status-test",
            amount: 100,
            currency: "USD",
            status: "Failed",
            timestamp: "2026-08-16T10:00:00Z",
        });

        const status = await screen.findByText("Failed");

        expect(status).toHaveClass("status-badge");
        expect(status).toHaveClass("status-failed");
    });
    it("should handle 100 transactions received rapidly", async () => {
        render(<MonitorPage />);

        for (let i = 0; i < 100; i++) {
            transactionReceivedHandler?.({
                transactionId: `transaction-${i}`,
                amount: i + 1,
                currency: "USD",
                status: "Completed",
                timestamp: new Date(
                    Date.UTC(2026, 7, 16, 10, 0, i)
                ).toISOString(),
            });
        }

        expect(
            await screen.findByText("transaction-99")
        ).toBeInTheDocument();

        expect(
            screen.getByText("transaction-0")
        ).toBeInTheDocument();

        expect(
            screen.getAllByRole("row")
        ).toHaveLength(101);
    });
    it("should connect to SignalR using WebSockets without negotiation", () => {
        render(<MonitorPage />);

        expect(withUrlMock).toHaveBeenCalledWith(
            expect.any(String),
            {
                transport: 1,
                skipNegotiation: true,
            }
        );
    });
    it("should update an existing transaction when its status changes", async () => {
    render(<MonitorPage />);

    transactionReceivedHandler?.({
        transactionId: "status-update",
        amount: 100,
        currency: "USD",
        status: "Pending",
        timestamp: "2026-08-20T10:00:00Z",
    });

    expect(
        await screen.findByText("Pending")
    ).toBeInTheDocument();

    transactionReceivedHandler?.({
        transactionId: "status-update",
        amount: 100,
        currency: "USD",
        status: "Completed",
        timestamp: "2026-08-20T10:00:00Z",
    });

    expect(
        await screen.findByText("Completed")
    ).toBeInTheDocument();

    expect(
        screen.queryByText("Pending")
    ).not.toBeInTheDocument();

    expect(
        screen.getAllByText("status-update")
    ).toHaveLength(1);
});
});