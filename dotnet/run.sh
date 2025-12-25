#!/bin/bash

# Function to display the menu
display_menu() {
    echo "Select an option:"
    echo "1) Build the solution"
    echo "2) Exit"
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
            echo "Exiting..."
            exit 0
            ;;
        *)
            echo "Invalid option. Please try again."
            ;;
    esac
    echo ""
done