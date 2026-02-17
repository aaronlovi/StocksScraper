#!/bin/bash

PROJECT="./Stocks.EDGARScraper.csproj"
WORKING_DIR="./Stocks.EDGARScraper"
OUTPUT_FILE="/mnt/c/temp/1.html"
CIK="320193"
CONCEPT="StatementOfFinancialPositionAbstract"
DATE="2025-03-29"
FORMAT="html"
ROLE="104000 - Statement - Statement of Financial Position, Classified"

# Function to display the menu
display_menu() {
    echo "Select an option:"
    echo "t) Run .NET tests"
    echo "u) Run Angular tests"
    echo "s) Start Web API + Angular dev server"
    echo "w) Start .NET Web API"
    echo "a) Start Angular dev server"
    echo "1) Build the solution"
    echo "2) Run the application with specific parameters"
    echo "3) Download SEC ticker mappings"
    echo "4) Download Stooq prices (batch CSV)"
    echo "5) Import price CSVs into the database"
    echo "6) Import bulk Stooq files into the database"
    echo "q) Exit"
}

# Function to build the solution
build_solution() {
    echo "Building the solution..."
    dotnet build
    if [ $? -eq 0 ]; then
        echo "Build succeeded."
    else
        echo "Build failed."
    fi
}

# Main script loop
while true; do
    display_menu
    read -p "Enter your choice: " choice

    case $choice in
        t)
            echo "Running .NET tests..."
            dotnet test EDGARScraper.sln
            if [ $? -eq 0 ]; then
                echo -e "\n=== .NET TESTS: PASS ==="
            else
                echo -e "\n=== .NET TESTS: FAIL ==="
            fi
            ;;
        u)
            echo "Running Angular tests..."
            pushd ../frontend/stocks-frontend > /dev/null
            npx ng test --watch=false
            ANGULAR_EXIT=$?
            popd > /dev/null
            if [ $ANGULAR_EXIT -eq 0 ]; then
                echo -e "\n=== ANGULAR TESTS: PASS ==="
            else
                echo -e "\n=== ANGULAR TESTS: FAIL ==="
            fi
            ;;
        s)
            echo "Starting .NET Web API + Angular dev server..."
            dotnet run --project Stocks.WebApi &
            DOTNET_PID=$!
            pushd ../frontend/stocks-frontend > /dev/null
            npx ng serve &
            NG_PID=$!
            popd > /dev/null
            echo "Web API PID: $DOTNET_PID, Angular PID: $NG_PID"
            echo "Press Enter to stop both..."
            read
            kill $DOTNET_PID $NG_PID 2>/dev/null
            wait $DOTNET_PID $NG_PID 2>/dev/null
            ;;
        w)
            echo "Starting .NET Web API..."
            dotnet run --project Stocks.WebApi
            ;;
        a)
            echo "Starting Angular dev server..."
            pushd ../frontend/stocks-frontend > /dev/null
            npx ng serve
            popd > /dev/null
            ;;
        1)
            build_solution
            ;;
        2)
            echo "Running the application..."
            pushd $WORKING_DIR > /dev/null
            dotnet run --project $PROJECT \
                -- --print-statement --cik $CIK --concept $CONCEPT --date $DATE \
                --format $FORMAT --role "$ROLE" > $OUTPUT_FILE
            if [ $? -ne 0 ]; then
                echo "The application failed to run. Check the output for errors."
            fi
            popd > /dev/null
            ;;
        3)
            echo "Downloading SEC ticker mappings..."
            pushd $WORKING_DIR > /dev/null
            dotnet run --project $PROJECT -- --download-sec-ticker-mappings
            if [ $? -ne 0 ]; then
                echo "The application failed to run. Check the output for errors."
            else
                DOWNLOAD_DIR=$(rg -n "\"EdgarDataDir\"" appsettings.json | sed -E 's/.*: \"([^\"]+)\".*/\1/')
                if [ -n "$DOWNLOAD_DIR" ]; then
                    echo "Downloaded ticker mappings to: $DOWNLOAD_DIR"
                    TICKERS_FILE="$DOWNLOAD_DIR/company_tickers.json"
                    EXCHANGE_FILE="$DOWNLOAD_DIR/company_tickers_exchange.json"
                    if [ -f "$TICKERS_FILE" ]; then
                        echo "  - $TICKERS_FILE"
                    else
                        echo "  - Missing: $TICKERS_FILE"
                    fi
                    if [ -f "$EXCHANGE_FILE" ]; then
                        echo "  - $EXCHANGE_FILE"
                    else
                        echo "  - Missing: $EXCHANGE_FILE"
                    fi
                fi
            fi
            popd > /dev/null
            ;;
        4)
            echo "Downloading Stooq prices..."
            pushd $WORKING_DIR > /dev/null
            dotnet run --project $PROJECT -- --download-prices-stooq
            if [ $? -ne 0 ]; then
                echo "The application failed to run. Check the output for errors."
            else
                OUTPUT_DIR=$(rg -n "\"OutputDir\"" appsettings.json | sed -E 's/.*: \"([^\"]+)\".*/\1/')
                if [ -z "$OUTPUT_DIR" ]; then
                    DATA_DIR=$(rg -n "\"EdgarDataDir\"" appsettings.json | sed -E 's/.*: \"([^\"]+)\".*/\1/')
                    if [ -n "$DATA_DIR" ]; then
                        OUTPUT_DIR="$DATA_DIR/prices/stooq"
                    fi
                fi
                if [ -n "$OUTPUT_DIR" ]; then
                    if [ -d "$OUTPUT_DIR" ]; then
                        echo "  - $OUTPUT_DIR"
                    else
                        echo "  - Missing: $OUTPUT_DIR"
                    fi
                fi
            fi
            popd > /dev/null
            ;;
        5)
            echo "Importing price CSVs..."
            pushd $WORKING_DIR > /dev/null
            dotnet run --project $PROJECT -- --import-prices-stooq
            if [ $? -ne 0 ]; then
                echo "The application failed to run. Check the output for errors."
            fi
            popd > /dev/null
            ;;
        6)
            echo "Importing bulk Stooq files..."
            pushd $WORKING_DIR > /dev/null
            dotnet run --project $PROJECT -- --import-prices-stooq-bulk
            if [ $? -ne 0 ]; then
                echo "The application failed to run. Check the output for errors."
            fi
            popd > /dev/null
            ;;
        q)
            echo "Exiting..."
            exit 0
            ;;
        *)
            echo "Invalid option. Please try again."
            ;;
    esac
    echo ""
done
