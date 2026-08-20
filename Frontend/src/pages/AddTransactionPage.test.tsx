import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import {
    afterEach,
    describe,
    expect,
    it,
    vi,
} from "vitest";

import AddTransactionPage from "./AddTransactionPage";

describe("AddTransactionPage", () => {
    afterEach(() => {
        vi.restoreAllMocks();
    });

    it("should send the values entered by the user", async () => {
        const fetchMock = vi
            .spyOn(globalThis, "fetch")
            .mockResolvedValue(
                new Response(
                    JSON.stringify({}),
                    {
                        status: 200,
                        headers: {
                            "Content-Type": "application/json",
                        },
                    }
                )
            );

        render(<AddTransactionPage />);

        await userEvent.type(
            screen.getByLabelText(/amount/i),
            "250.75"
        );

        await userEvent.selectOptions(
            screen.getByLabelText(/currency/i),
            "EUR"
        );

        await userEvent.selectOptions(
            screen.getByLabelText(/status/i),
            "Failed"
        );

        await userEvent.click(
            screen.getByRole("button", {
                name: /send transaction/i,
            })
        );

        expect(fetchMock).toHaveBeenCalledTimes(1);

        const [url, options] = fetchMock.mock.calls[0];

        expect(url).toBe(
            "http://localhost:5032/api/transactions"
        );

        expect(options).toEqual(
            expect.objectContaining({
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
            })
        );

        const body = JSON.parse(options?.body as string);

        expect(body.amount).toBe(250.75);
        expect(body.currency).toBe("EUR");
        expect(body.status).toBe("Failed");

        expect(body.transactionId).toBeTruthy();
        expect(body.timestamp).toBeTruthy();
    });

    it("should not send transaction when amount is empty", async () => {
        const fetchMock = vi
            .spyOn(globalThis, "fetch")
            .mockResolvedValue(new Response());

        render(<AddTransactionPage />);

        await userEvent.click(
            screen.getByRole("button", {
                name: /send transaction/i,
            })
        );

        expect(fetchMock).not.toHaveBeenCalled();

        expect(
            screen.getByText(
                /amount must be greater than zero/i
            )
        ).toBeInTheDocument();
    });

    it("should show success message when transaction is sent successfully", async () => {
        vi.spyOn(globalThis, "fetch")
            .mockResolvedValue(
                new Response(
                    JSON.stringify({}),
                    {
                        status: 200,
                        headers: {
                            "Content-Type": "application/json",
                        },
                    }
                )
            );

        render(<AddTransactionPage />);

        await userEvent.type(
            screen.getByLabelText(/amount/i),
            "100"
        );

        await userEvent.click(
            screen.getByRole("button", {
                name: /send transaction/i,
            })
        );

        expect(
            await screen.findByRole("status")
        ).toHaveTextContent(
            "Transaction sent successfully."
        );

        expect(
            screen.getByLabelText(/amount/i)
        ).toHaveValue(null);
    });

    it("should show error message when backend request fails", async () => {
        vi.spyOn(globalThis, "fetch")
            .mockResolvedValue(
                new Response(
                    JSON.stringify({}),
                    {
                        status: 500,
                        headers: {
                            "Content-Type": "application/json",
                        },
                    }
                )
            );

        render(<AddTransactionPage />);

        await userEvent.type(
            screen.getByLabelText(/amount/i),
            "100"
        );

        await userEvent.click(
            screen.getByRole("button", {
                name: /send transaction/i,
            })
        );

        expect(
            await screen.findByRole("alert")
        ).toHaveTextContent(
            "Failed to send transaction."
        );

        expect(
            screen.queryByRole("status")
        ).not.toBeInTheDocument();
    });
});