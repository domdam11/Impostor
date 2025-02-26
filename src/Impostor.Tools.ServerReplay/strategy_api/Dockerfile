# Stage 2: Runtime
FROM ubuntu:24.04

# Install runtime dependencies
RUN apt-get update && apt-get install -y \
    libboost-all-dev \
    libasio-dev \
    && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Copy the binary from the local `./bin` folder to the container
COPY ./bin/Argumentation /app/Argumentation

# Expose the port (if the application uses a specific port, e.g., 8080)
EXPOSE 18080

# Command to run the binary
CMD ["./Argumentation"]