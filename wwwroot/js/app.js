/**
 * Barometer Dashboard - Main Application Script
 * Real-time geopolitical monitoring with jQuery AJAX
 */

(function($) {
    'use strict';

    // ===================
    // Configuration
    // ===================
    const CONFIG = {
        weatherInterval: 60 * 60 * 1000,      // 60 minutes
        polymarketInterval: 5 * 60 * 1000,    // 5 minutes
        alertInterval: 45 * 1000,             // 45 seconds
        apiBaseUrl: '/api'
    };

    // ===================
    // State Management
    // ===================
    let state = {
        audioInitialized: false,
        lastAlertStatus: 'Safe',
        isConnected: false
    };

    // ===================
    // Audio Handling
    // ===================
    const alertSound = document.getElementById('alert-sound');

    function initializeAudio() {
        if (state.audioInitialized) return;
        
        // Create a silent play to unlock audio on iOS/Chrome
        if (alertSound) {
            alertSound.volume = 0;
            alertSound.play().then(() => {
                alertSound.pause();
                alertSound.currentTime = 0;
                alertSound.volume = 1;
                state.audioInitialized = true;
                console.log('Audio initialized successfully');
            }).catch(err => {
                console.log('Audio initialization pending:', err.message);
            });
        }
    }

    function playAlertSound() {
        if (alertSound && state.audioInitialized) {
            alertSound.currentTime = 0;
            alertSound.play().catch(err => {
                console.error('Failed to play alert sound:', err);
            });
        }
    }

    // ===================
    // UI Update Functions
    // ===================
    function updateConnectionStatus(connected, text) {
        const $dot = $('#connection-status');
        const $text = $('#connection-text');
        
        $dot.removeClass('connected error');
        if (connected) {
            $dot.addClass('connected');
            state.isConnected = true;
        } else {
            $dot.addClass('error');
            state.isConnected = false;
        }
        $text.text(text || (connected ? 'Connected' : 'Disconnected'));
    }

    function updateLastUpdateTime() {
        const now = new Date();
        const timeStr = now.toLocaleTimeString('en-US', { 
            hour: '2-digit', 
            minute: '2-digit',
            second: '2-digit'
        });
        $('#last-update-text').text(`Last update: ${timeStr}`);
    }

    function formatTime(timestamp) {
        const date = new Date(timestamp);
        return {
            hour: date.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' }),
            date: date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
        };
    }

    function getWeatherIconUrl(iconCode) {
        return `https://openweathermap.org/img/wn/${iconCode}@2x.png`;
    }

    // ===================
    // Weather Panel
    // ===================
    function fetchWeather() {
        $.ajax({
            url: `${CONFIG.apiBaseUrl}/weather`,
            method: 'GET',
            dataType: 'json',
            success: function(data) {
                renderWeather(data);
                updateCacheBadge('#weather-cache-badge', data.cachedAt, data.isFromCache);
                updateConnectionStatus(true, 'Connected');
            },
            error: function(xhr, status, error) {
                console.error('Weather fetch error:', error);
                renderWeatherError();
            }
        });
    }

    function renderWeather(data) {
        const $container = $('#weather-forecast');
        
        if (!data.forecasts || data.forecasts.length === 0) {
            $container.html(`
                <div class="error-state">
                    <i class="bi bi-cloud-slash"></i>
                    <p>No forecast data available</p>
                </div>
            `);
            return;
        }

        let html = '';
        data.forecasts.forEach(forecast => {
            const time = formatTime(forecast.timestamp);
            const iconUrl = getWeatherIconUrl(forecast.icon);
            
            html += `
                <div class="forecast-item">
                    <div class="forecast-time">
                        <div class="hour">${time.hour}</div>
                        <div class="date">${time.date}</div>
                    </div>
                    <div class="forecast-icon">
                        <img src="${iconUrl}" alt="${forecast.description}">
                    </div>
                    <div class="forecast-temp">
                        <div class="temp">${Math.round(forecast.temperature)}°C</div>
                        <div class="feels-like">Feels ${Math.round(forecast.feelsLike)}°</div>
                    </div>
                    <div class="forecast-details">
                        <div class="description">${forecast.description}</div>
                        <div class="stats">
                            <i class="bi bi-droplet"></i> ${forecast.humidity}%
                            <i class="bi bi-wind ms-2"></i> ${forecast.windSpeed} m/s
                        </div>
                    </div>
                </div>
            `;
        });
        
        $container.html(html);
    }

    function renderWeatherError() {
        $('#weather-forecast').html(`
            <div class="error-state">
                <i class="bi bi-exclamation-triangle"></i>
                <p>Failed to load weather data</p>
                <small>Retrying...</small>
            </div>
        `);
    }

    // ===================
    // Polymarket Panel
    // ===================
    function fetchPolymarket() {
        $.ajax({
            url: `${CONFIG.apiBaseUrl}/polymarket`,
            method: 'GET',
            dataType: 'json',
            success: function(data) {
                renderPolymarket(data);
                updateCacheBadge('#polymarket-cache-badge', data.cachedAt, data.isFromCache);
            },
            error: function(xhr, status, error) {
                console.error('Polymarket fetch error:', error);
                renderPolymarketError();
            }
        });
    }

    function renderPolymarket(data) {
        const $container = $('#polymarket-data');
        const $title = $('#market-title');
        
        $title.text(data.marketTitle || 'US Strikes Iran');
        
        const yesPercent = data.yesPercentage.toFixed(1);
        const noPercent = data.noPercentage.toFixed(1);
        const volumeFormatted = formatVolume(data.volume);
        
        let bigTradeHtml = '';
        if (data.bigTradeDetected) {
            bigTradeHtml = `
                <div class="big-trade-badge">
                    <i class="bi bi-lightning-charge-fill"></i>
                    <span>Big Trade Detected (&gt;$20k)</span>
                </div>
            `;
        }
        
        $container.html(`
            <div class="prediction-cards">
                <div class="prediction-card yes-card">
                    <div class="prediction-label">Yes</div>
                    <div class="prediction-value">${yesPercent}%</div>
                </div>
                <div class="prediction-card no-card">
                    <div class="prediction-label">No</div>
                    <div class="prediction-value">${noPercent}%</div>
                </div>
            </div>
            ${bigTradeHtml}
            <div class="volume-info">
                <i class="bi bi-bar-chart-fill"></i> Total Volume: ${volumeFormatted}
            </div>
        `);
    }

    function formatVolume(volume) {
        if (!volume) return '$0';
        if (volume >= 1000000) {
            return '$' + (volume / 1000000).toFixed(2) + 'M';
        } else if (volume >= 1000) {
            return '$' + (volume / 1000).toFixed(1) + 'K';
        }
        return '$' + volume.toFixed(0);
    }

    function renderPolymarketError() {
        $('#polymarket-data').html(`
            <div class="error-state">
                <i class="bi bi-exclamation-triangle"></i>
                <p>Failed to load market data</p>
                <small>Retrying...</small>
            </div>
        `);
    }

    // ===================
    // Alert Panel
    // ===================
    function fetchAlerts() {
        $.ajax({
            url: `${CONFIG.apiBaseUrl}/alerts`,
            method: 'GET',
            dataType: 'json',
            success: function(data) {
                renderAlerts(data);
                updateCacheBadge('#alert-cache-badge', data.cachedAt, data.isFromCache);
                handleAlertTransition(data);
            },
            error: function(xhr, status, error) {
                console.error('Alerts fetch error:', error);
                renderAlertsError();
            }
        });
    }

    function renderAlerts(data) {
        const $container = $('#alert-data');
        const $panel = $('#alert-panel');
        
        // Remove previous state classes
        $panel.removeClass('major-alert');
        
        let statusClass = 'safe';
        let iconClass = 'bi-shield-check';
        let statusText = 'Safe';
        let statusDesc = 'No major alerts detected';
        
        if (data.isMajorAlert) {
            statusClass = 'major-alert';
            iconClass = 'bi-exclamation-triangle-fill';
            statusText = 'MAJOR ALERT';
            statusDesc = 'All three major cities under alert';
            $panel.addClass('major-alert');
        } else if (data.hasAnyAlert) {
            statusClass = 'alert';
            iconClass = 'bi-exclamation-circle';
            statusText = 'Alert Active';
            statusDesc = 'Alerts detected in some areas';
        }
        
        let citiesHtml = '';
        if (data.activeCitiesEnglish && data.activeCitiesEnglish.length > 0) {
            const majorCities = data.majorCitiesInAlert || [];
            const cityTags = data.activeCitiesEnglish.map(city => {
                const isMajor = majorCities.includes(city);
                return `<span class="city-tag ${isMajor ? 'major' : ''}">${city}</span>`;
            }).join('');
            
            citiesHtml = `
                <div class="alert-cities-list">
                    <h4><i class="bi bi-geo-alt"></i> Active Alert Cities:</h4>
                    <div class="cities-tags">${cityTags}</div>
                </div>
            `;
        }
        
        $container.html(`
            <div class="alert-status ${statusClass}">
                <div class="alert-icon">
                    <i class="bi ${iconClass}"></i>
                </div>
                <div class="alert-status-text">${statusText}</div>
                <div class="alert-status-desc">${statusDesc}</div>
            </div>
            ${citiesHtml}
        `);
    }

    function handleAlertTransition(data) {
        const currentStatus = data.isMajorAlert ? 'MajorAlert' : (data.hasAnyAlert ? 'Alert' : 'Safe');
        
        // Play sound only when transitioning from Safe to MajorAlert
        if (state.lastAlertStatus === 'Safe' && currentStatus === 'MajorAlert') {
            playAlertSound();
            console.log('Alert triggered: Safe -> MajorAlert');
        }
        
        state.lastAlertStatus = currentStatus;
    }

    function renderAlertsError() {
        $('#alert-data').html(`
            <div class="error-state">
                <i class="bi bi-exclamation-triangle"></i>
                <p>Failed to load alert status</p>
                <small>Retrying...</small>
            </div>
        `);
    }

    // ===================
    // Cache Badge Update
    // ===================
    function updateCacheBadge(selector, cachedAt, isFromCache) {
        const $badge = $(selector);
        if (!cachedAt) {
            $badge.html('<i class="bi bi-clock"></i> --');
            return;
        }
        
        const cached = new Date(cachedAt);
        const now = new Date();
        const diffMs = now - cached;
        const diffMins = Math.floor(diffMs / 60000);
        const diffSecs = Math.floor((diffMs % 60000) / 1000);
        
        let timeText;
        if (diffMins > 0) {
            timeText = `${diffMins}m ago`;
        } else {
            timeText = `${diffSecs}s ago`;
        }
        
        const cacheIcon = isFromCache ? 'bi-database-fill' : 'bi-cloud-download';
        $badge.html(`<i class="bi ${cacheIcon}"></i> ${timeText}`);
        $badge.removeClass('bg-secondary bg-success').addClass(isFromCache ? 'bg-secondary' : 'bg-success');
    }

    // ===================
    // Initialization
    // ===================
    function initDashboard() {
        // Remove overlay
        $('#audio-init-overlay').addClass('hidden');
        
        // Initialize audio
        initializeAudio();
        
        // Initial data fetch
        fetchWeather();
        fetchPolymarket();
        fetchAlerts();
        
        // Set up polling intervals
        setInterval(fetchWeather, CONFIG.weatherInterval);
        setInterval(fetchPolymarket, CONFIG.polymarketInterval);
        setInterval(fetchAlerts, CONFIG.alertInterval);
        
        // Update timestamp periodically
        setInterval(updateLastUpdateTime, 1000);
        
        console.log('Barometer Dashboard initialized');
    }

    // ===================
    // Event Handlers
    // ===================
    $(document).ready(function() {
        // Audio init button click
        $('#init-audio-btn').on('click', function() {
            initDashboard();
        });
        
        // Also initialize on any click (backup for mobile)
        $(document).one('click', function() {
            if (!state.audioInitialized) {
                initializeAudio();
            }
        });
        
        // Keyboard shortcut - press 'S' to trigger test alert sound
        $(document).on('keypress', function(e) {
            if (e.key === 's' || e.key === 'S') {
                if (state.audioInitialized) {
                    playAlertSound();
                    console.log('Test alert sound played');
                }
            }
        });
    });

})(jQuery);
