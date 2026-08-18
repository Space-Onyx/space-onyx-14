bank-program-ui-no-account = [color=red]No account linked.[/color]
bank-program-name = NanoBank
bank-program-ui-balance-label = Balance:
bank-program-ui-account-label = Account:
bank-program-ui-link-account = Link Account
bank-program-ui-account-number = Account Number
bank-program-ui-link-confirm = Link
bank-program-ui-link-cancel = Cancel
bank-program-ui-account-number-text = Account #{ $account }
bank-program-ui-account-owner-text = Account Owner: { $owner }
bank-program-ui-link-error = [color=red]Account link error.[/color]
bank-program-ui-link-success = [color=green]Account successfully linked.[/color]
bank-program-ui-link-program = Account will be linked to the program.
bank-program-ui-link-id-card = Account will be linked to ID card.
bank-program-ui-link-no-id-card = [color=red]No ID card found.[/color]
bank-program-ui-link-id-card-linked = [color=red]ID card already linked to account: { $account }[/color]

# PIN Change
bank-program-ui-change-pin-title = Change PIN
bank-program-ui-old-pin = Old PIN
bank-program-ui-new-pin = New PIN
bank-program-ui-save-pin = Save PIN
bank-program-ui-change-pin-success = [color=green]PIN successfully changed.[/color]
bank-program-ui-change-pin-error = [color=red]PIN change error.[/color]
bank-program-ui-change-pin-wrong-old = [color=red]Wrong old PIN.[/color]
bank-program-ui-change-pin-invalid = [color=red]Invalid PIN format.[/color] 

bank-program-ui-transfer-title = Transfer Funds
bank-program-ui-transfer-account = Recipient Account Number
bank-program-ui-transfer-received = [color=green]You have received { $amount } credits from { $from }. { $comment }[/color]
bank-program-ui-transfer-comment = Comment (Optional)
bank-program-ui-transfer-confirm = Transfer
bank-program-ui-transfer-cancel = Cancel
bank-program-ui-transfer-success = [color=green]Successfully transferred { $amount } credits to account #{ $to }.[/color]
bank-program-ui-transfer-received-chat = [color=green]You have received { $amount } credits from { $from }.[/color]
bank-program-ui-transfer-error-no-from = [color=red]Error: Source account not found.[/color]
bank-program-ui-transfer-error-self = [color=red]Error: Cannot transfer to your own account.[/color]
bank-program-ui-transfer-error-no-to = [color=red]Error: Target account not found.[/color]
bank-program-ui-transfer-amount = Amount
bank-program-ui-transfer-pin = PIN code
bank-program-ui-transfer-error-amount = [color=red]Error: Invalid transfer amount.[/color]
bank-program-ui-transfer-error-nomoney = [color=red]Error: Not enough funds to transfer.[/color]
bank-program-ui-transfer-error-pin = [color=red]Incorrect PIN.[/color]

# ATM
bank-program-ui-balance = Balance: { $balance }

# Transaction History
bank-program-ui-refresh-tooltip = Refresh
bank-program-ui-history-search-placeholder = Search history...
bank-program-ui-exit-history = Exit History
bank-program-ui-transaction-history = Transaction History
bank-program-ui-transaction-comment = Comment: { $comment }
bank-program-ui-salary-description = Salary Payment
bank-program-ui-transaction-entry = [color={ $color }]{ $amount }[/color] - { $description } ({ $timestamp }){ $comment }
bank-program-ui-transaction-transfer-sent = Transfer to account { $account } ({ $name })
bank-program-ui-transaction-transfer-received = Received from account { $account } ({ $name })
bank-program-ui-transaction-purchase-sent = Cashless payment to account { $account } ({ $name })
bank-program-ui-transaction-purchase-received = Cashless payment from account { $account } ({ $name })
bank-program-ui-transaction-deposit-atm = Deposit via ATM
bank-program-ui-transaction-withdraw-atm = Withdrawal via ATM
bank-program-ui-transaction-vending-purchase = Vending machine purchase: { $item }
