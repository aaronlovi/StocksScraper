#!/bin/bash

PROJECT="./Stocks.EDGARScraper.csproj"
WORKING_DIR="./Stocks.EDGARScraper"
OUTPUT_FILE="/tmp/1.html"
CIK="320193"
CONCEPT="StatementOfFinancialPositionAbstract"
DATE="2025-03-29"
FORMAT="html"
ROLE="104000 - Statement - Statement of Financial Position, Classified"

# Function to display the menu
display_menu() {
    echo "Select an option:"
    echo "1) Build the solution"
    echo "2) Run the application with specific parameters"
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