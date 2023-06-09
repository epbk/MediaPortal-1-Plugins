﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Plugins.WorldWeatherLite.Language
{
    public enum TranslationEnum
    {
        unknown = -1,
        buttonAction = 0,
        buttonLocation = 1,
        buttonBrowserMap = 2,
        buttonView = 3,
        buttonRefresh = 9,
        labelCondition = 10,
        labelMedia = 11,
        labelGeoClock = 12,
        labelAstronomy = 13,
        labelCalendar = 14,
        contextAddLocation = 20,
        contextManuallyAddLocation = 21,
        contextChangeCurrentConditionProvider = 22,
        contextChangeForecastConditionProvider = 23,
        contextChangeForecastHourConditionProvider = 24,
        contextChangeHistoryYearConditionProvider = 25,
        contextChangeHistoryDayConditionProvider = 26,
        contextChangeMapProvider = 27,
        contextChangeChartStyle = 28,
        contextChangeIconCollection = 29,
        headerMenu = 30,
        headerProvider = 0x1F,
        headerLocation = 0x20,
        headerMediaView = 33,
        headerAstronomyView = 34,
        headerConditionView = 35,
        headerIconCollection = 36,
        headerBrowserMap = 37,
        headerChartStyle = 38,
        contextOpenMainBrowserMap = 40,
        notificationInvalidLocation = 45,
        notificationInvalidProviderKey = 46,
        notificationAnyWorkerIsBusy = 47,
        notificationDetectedLocation = 48,
        notificationNoInternet = 49,
        questionAddLocation = 50,
        questionOverwriteLocation = 51,
        questionChangeConditionProvider = 52,
        questionChangeMapProvider = 53,
        questionChangeIconCollection = 54,
        questionChangeChartStyle = 55,
        errorFailedLocationDetection = 60,
        errorWeatherCondition = 61,
        errorMedia = 62,
        errorGeoClockImage = 0x3F,
        errorStarrySkyImage = 0x40,
        errorPictureOfDayImage = 65,
        errorZodiacImage = 66,
        errorWeatherMap = 67,
        errorInvalidCityNameOrCode = 68,
        errorInvalidLongitudeLatitude = 69,
        labelConditionSunny = 70,
        labelConditionMostlySunny = 71,
        labelConditionCloudy = 72,
        labelConditionMostlyCloudy = 73,
        labelConditionPartlyCloudy = 74,
        labelConditionRain = 75,
        labelConditionChanceOfRain = 76,
        labelConditionShowers = 77,
        labelConditionSnow = 78,
        labelConditionChanceOfSnow = 79,
        labelConditionStorm = 80,
        labelConditionChanceOfStorm = 81,
        labelConditionThunderStorm = 82,
        labelConditionChanceOfThunderStorm = 83,
        labelConditionFlurries = 84,
        labelConditionSleet = 85,
        labelConditionIce = 86,
        labelConditionFog = 87,
        labelConditionSmoke = 88,
        labelConditionHaze = 89,
        labelConditionMist = 90,
        labelConditionDust = 91,
        labelConditionFair = 92,
        labelConditionHail = 93,
        labelConditionBlustery = 94,
        labelConditionWindy = 95,
        labelConditionHot = 96,
        labelConditionCold = 97,
        labelConditionTornado = 98,
        labelConditionTropicalStorm = 99,
        labelConditionSeverThunderStorm = 100,
        labelConditionIsolatedThunderStorm = 101,
        labelConditionScatteredThunderStorm = 102,
        labelConditionThunderShower = 103,
        labelConditionMixedRainAndSnow = 104,
        labelConditionMixedRainAndSleet = 105,
        labelConditionMixedRainAndHail = 106,
        labelConditionMixedSnowAndSleet = 107,
        labelConditionDrizzle = 108,
        labelConditionFreezingDrizzle = 109,
        labelConditionFreezingRain = 110,
        labelConditionScatteredShowers = 111,
        labelConditionSnowShowers = 112,
        labelConditionBlowingSnow = 113,
        labelConditionHeavySnow = 114,
        labelConditionSnowFlurries = 115,
        labelConditionIsolatedThundershowers = 116,
        labelConditionLightSnowShowers = 117,
        labelConditionClear = 118,
        labelConditionClearSunny = 119,
        labelConditionOvercast = 120,
        labelConditionThunderyOutbreaks = 121,
        labelConditionBlizzard = 122,
        labelConditionFreezingFog = 123,
        labelConditionLightDrizzle = 124,
        labelConditionLightRain = 125,
        labelConditionHeavyRain = 126,
        labelConditionLightFreezingRain = 0x7F,
        labelConditionLightSleet = 0x80,
        labelConditionLightSnow = 129,
        labelConditionIcePellets = 130,
        labelConditionLightShowersOfIcePellets = 131,
        labelConditionShowersOfIcePellets = 132,
        labelConditionLightSleetShowers = 133,
        labelConditionSleetShowers = 134,
        labelConditionTorrentialRainShowers = 135,
        labelConditionRainInAreaWithThunder = 136,
        labelConditionSnowInAreaWithThunder = 137,
        labelConditionHeavySnowShowers = 138,
        labelConditionLightShowers = 139,
        labelConditionHeavyShowers = 140,
        labelConditionLightStorm = 141,
        labelConditionHeavyStorm = 142,
        labelConditionHeavyDrizzle = 143,
        labelConditionChanceOfDrizzle = 144,
        labelConditionChanceOfSnowFlurries = 145,
        labelConditionChanceOfSnowShowers = 146,
        labelConditionChanceOfLightSnow = 147,
        labelConditionChanceOfSleet = 148,
        labelConditionChanceOfFreezingRain = 149,
        labelConditionChanceOfMixedRainAndSnow = 150,
        labelConditionChanceOfFreezingDrizzle = 151,
        labelConditionScatteredSnowShowers = 152,
        labelConditionPartlySunny = 153,
        labelConditionMixedRainAndMist = 154,
        labelConditionMixedRainAndThunderstorms = 155,
        labelConditionChanceOfFlurries = 156,
        labelConditionMixedSnowAndMist = 157,
        labelConditionMixedSnowAndThunderstorms = 158,
        labelConditionIceShowers = 159,
        labelConditionMixedIceAndThunderstorms = 160,
        labelConditionHailShowers = 161,
        labelConditionMixedHailAndThunderstorms = 162,
        labelConditionSand = 163,
        labelConditionSandstorms = 164,
        labelConditionVolcanicAsh = 165,
        labelConditionCalm = 166,
        labelWeekdayMonday = 170,
        labelWeekdayTuesday = 171,
        labelWeekdayWednesday = 172,
        labelWeekdayThursday = 173,
        labelWeekdayFriday = 174,
        labelWeekdaySaturday = 175,
        labelWeekdaySunday = 176,
        labelWindDirectionNorth = 180,
        labelWindDirectionNorthNorthEast = 181,
        labelWindDirectionNorthEast = 182,
        labelWindDirectionEastNorthEast = 183,
        labelWindDirectionEast = 184,
        labelWindDirectionEastSouthEast = 185,
        labelWindDirectionSouthEast = 186,
        labelWindDirectionSouthSouthEast = 187,
        labelWindDirectionSouth = 188,
        labelWindDirectionSouthSouthWest = 189,
        labelWindDirectionSouthWest = 190,
        labelWindDirectionWestSouthWest = 191,
        labelWindDirectionWest = 192,
        labelWindDirectionWestNorthWest = 193,
        labelWindDirectionNorthWest = 194,
        labelWindDirectionNorthNorthWest = 195,
        labelWindScaleCalm = 200,
        labelWindScaleLightAir = 201,
        labelWindScaleLightBreeze = 202,
        labelWindScaleGentleBreeze = 203,
        labelWindScaleModerateBreeze = 204,
        labelWindScaleFreshBreeze = 205,
        labelWindScaleStrongBreeze = 206,
        labelWindScaleNearGale = 207,
        labelWindScaleGale = 208,
        labelWindScaleStrongGale = 209,
        labelWindScaleStorm = 210,
        labelWindScaleViolentStorm = 211,
        labelWindScaleHurricane = 212,
        labelUVIndexLow = 220,
        labelUVIndexMedium = 221,
        labelUVIndexHigh = 222,
        labelUVIndexVeryHigh = 223,
        labelMoonPhaseNewMoon = 230,
        labelMoonPhaseWaxingCrescent = 231,
        labelMoonPhaseFirstQuarter = 232,
        labelMoonPhaseWaxingGibbous = 233,
        labelMoonPhaseFullMoon = 234,
        labelMoonPhaseWaningGibbous = 235,
        labelMoonPhaseLastQuarter = 236,
        labelMoonPhaseWaningCrescent = 237,
        viewImageFlat = 250,
        viewImageFilmstrip = 251,
        viewImageCoverflow = 252,
        viewImageThumbnail = 253,
        viewAstronomyNormal = 0xFF,
        viewAstronomyImage = 0x100,
        viewConditionNormal = 260,
        viewConditionHour = 261,
        viewConditionGraphic = 262,
        viewConditionHistory = 263,
        labelEmpty = 270,
        labelUnlimited = 271,
        labelNotVisible = 272,
        unitCelsius = 300,
        unitFahrenheit = 301,
        unitKelvin = 302,
        unitRankine = 303,
        unitNewton = 304,
        unitKilometersPerHour = 310,
        unitMeterPerSecond = 311,
        unitMilesPerHour = 312,
        unitKnots = 313,
        unitBeaufort = 314,
        unitKilometer = 320,
        unitMile = 321,
        unitMillibar = 330,
        unitHektoPascal = 331,
        unitPoundsPerSquareInch = 332,
        unitTorr = 333,
        unitInch = 334,
        unitBarometricRising = 340,
        unitBarometricSteady = 341,
        unitBarometricFalling = 342,
        unitBarometricRisingOrSteadyThenFalling = 343,
        unitBarometricRisingThenFalling = 344,
        unitBarometricRisingThenSteady = 345,
        unitBarometricFallingOrSteadyThenRising = 346,
        unitBarometricFallingThenRising = 347,
        unitBarometricFallingThenSteady = 348,
        unitDegree = 350,
        unitPercent = 351,
        unitMinuteOfArc = 352,
        unitMillimeter = 360,
        metricsSI = 370,
        metricsUS = 371,
        labelChartStyleLine = 380,
        labelChartStyleCurve = 381,
        labelChartStylePoint = 382,
        labelChartStyleBar = 383,
        labelChartStylePeak = 384,
        wizardLocationCity = 400,
        wizardLocationCityCode = 401,
        wizardLocationCityPostalCode = 402,
        wizardLocationCountry = 403,
        wizardTimezone = 404,
        wizardMetric = 405,
        translationWeek = 470,
        translationDay = 471,
        translationYear = 472,
        translationHour = 473,
        translationJulianDate = 474,
        translationCivilTwilightMorning = 475,
        translationCivilTwilightEvening = 476,
        translationSunrise = 480,
        translationSunset = 481,
        translationSunCulmination = 482,
        translationSunAltitude = 483,
        translationSunAzimuth = 484,
        translationSunDiameter = 485,
        translationSunDistance = 486,
        translationMoonrise = 490,
        translationMoonset = 491,
        translationMoonCulmination = 492,
        translationMoonAltitude = 493,
        translationMoonAzimuth = 494,
        translationMoonDiameter = 495,
        translationMoonDistance = 496,
        translationMoonPhase = 497,
        translationTemperature = 500,
        translationTemperatureAverage = 501,
        translationTemperatureRecord = 502,
        translationTemperatureFeelsLike = 503,
        translationWind = 504,
        translationWindSpeed = 505,
        translationHumidity = 506,
        translationDaylight = 507,
        translationUVIndex = 508,
        translationHeatIndex = 509,
        translationPrecipitation = 510,
        translationSunshineDuration = 0x1FF,
        translationPressure = 0x200,
        translationBarometricPressure = 513,
        translationVisibility = 514,
        translationDewPoint = 515,
        translationTemperatureLow = 516,
        translationTemperatureHigh = 517,
        translationCloudCoverage = 518,
        translationFogCoverage = 519,
        translationLocation = 520,
        translationCondition = 521,
        translationCurrentCondition = 522,
        translationForecastCondition = 523,
        translationHistoryYearCondition = 524,
        translationHistoryDayCondition = 525,
        translationSatelliteMedia = 530,
        translationTemperatureMedia = 531,
        translationUVIndexMedia = 532,
        translationWindMedia = 533,
        translationPrecipitationMedia = 534,
        translationSuntimeMedia = 535,
        translationPollenCountMedia = 536,
        translationWorldMedia = 537,
        translationWebcamMedia = 538,
        translationHumidityMedia = 539,
        translationSelfDefinedMedia = 540,
        translationGeoClock = 545,
        translationStarrySky = 550,
        translationPictureOfDay = 551,
        translationZodiac = 552,
        translationFeed = 560,
        translationChart = 565,
        translationHoliday = 570,
        translationNewYear = 571,
        translationEpiphany = 572,
        translationAssumptionDay = 573,
        translationReformationDay = 574,
        translationAllSaintsDay = 575,
        translationEasterSunday = 576,
        translationHolyThursday = 577,
        translationGoodFriday = 578,
        translationAscensionDay = 579,
        translationWhitSunday = 580,
        translationCorpusChristi = 581,
        translationChristmasDay = 582,
        translationSeason = 590,
        translationSpringSeason = 591,
        translationSummerSeason = 592,
        translationAutumnSeason = 593,
        translationWinterSeason = 594,
        translationCapricorn = 600,
        translationAquarius = 601,
        translationPisces = 602,
        translationAries = 603,
        translationTaurus = 604,
        translationGemini = 605,
        translationCancer = 606,
        translationLeo = 607,
        translationVirgo = 608,
        translationLibra = 609,
        translationScorpio = 610,
        translationSagittarius = 611,
        translationProviderText = 700,
        translationXMLFileProvider = 701,
        translationNoProvider = 702,
        translationRefreshDateTime = 710,
        translationRefreshNextDateTime = 711
    }

}
