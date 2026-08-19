package com.fromthefarm.app.ui.navigation

import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CalendarToday
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.List
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.navigation.NavDestination.Companion.hierarchy
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.fromthefarm.app.ui.screens.*
import com.fromthefarm.app.ui.theme.FarmGreen
import androidx.compose.runtime.getValue
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import com.fromthefarm.app.data.UserRole

// Routes
private const val LOGIN = "login"
private const val ONBOARDING = "onboarding"
private const val HOME = "home"
private const val LISTINGS = "listings"
private const val CREATE_LISTING = "createListing"
private const val MATCH_DETAIL = "matchDetail"
private const val DEMAND_BOARD = "demandBoard"
private const val CREATE_DEMAND = "createDemand"
private const val CALENDAR = "calendar"
private const val SETTINGS = "settings"

// Routes that show the bottom navigation bar
private val bottomBarRoutes = setOf(HOME, LISTINGS, CALENDAR, SETTINGS)

private data class BottomItem(val route: String, val label: String, val icon: androidx.compose.ui.graphics.vector.ImageVector)

private val bottomItems = listOf(
    BottomItem(HOME, "Home", Icons.Filled.Home),
    BottomItem(LISTINGS, "Listings", Icons.Filled.List),
    BottomItem(CALENDAR, "Calendar", Icons.Filled.CalendarToday),
    BottomItem(SETTINGS, "Settings", Icons.Filled.Settings)
)

@Composable
fun FarmNavHost() {
    val navController = rememberNavController()
    var userRole by remember { mutableStateOf(UserRole.FARMER) }
    val backStackEntry by navController.currentBackStackEntryAsState()
    val currentRoute = backStackEntry?.destination?.route

    Scaffold(
        bottomBar = {
            if (currentRoute in bottomBarRoutes) {
                NavigationBar {
                    bottomItems.forEach { item ->
                        NavigationBarItem(
                            selected = currentRoute == item.route,
                            onClick = {
                                navController.navigate(item.route) {
                                    popUpTo(navController.graph.findStartDestination().id) { saveState = true }
                                    launchSingleTop = true
                                    restoreState = true
                                }
                            },
                            icon = { Icon(item.icon, contentDescription = item.label) },
                            label = { Text(item.label) },
                            colors = NavigationBarItemDefaults.colors(selectedIconColor = FarmGreen, indicatorColor = FarmGreen.copy(alpha = 0.15f))
                        )
                    }
                }
            }
        }
    ) { padding ->
        NavHost(
            navController = navController,
            startDestination = LOGIN,
            modifier = Modifier.padding(padding)
        ) {
            composable(LOGIN) {
                LoginScreen(onContinueWithGoogle = { navController.navigate(ONBOARDING) })
            }
            composable(ONBOARDING) {
                OnboardingScreen(onContinue = { selectedRole ->
                    userRole = selectedRole
                    navController.navigate(HOME) { popUpTo(LOGIN) { inclusive = true } }
                })
            }
            composable(HOME) {
                HomeScreen(onOpenMatch = { navController.navigate(MATCH_DETAIL) })
            }
            composable(LISTINGS) {
                if (userRole == UserRole.FARMER) {
                    MyListingsScreen(
                        onAddListing = { navController.navigate(CREATE_LISTING) },
                        onOpenListing = { navController.navigate(MATCH_DETAIL) }
                    )
                } else {
                    BuyerDemandBoardScreen(
                        onAddRequest = { navController.navigate(CREATE_DEMAND) }
                    )
                }
            }
            composable(CREATE_LISTING) {
                CreateListingScreen(onBack = { navController.popBackStack() }, onSave = { navController.popBackStack() })
            }
            composable(MATCH_DETAIL) {
                MatchDetailScreen(onBack = { navController.popBackStack() })
            }
            composable(DEMAND_BOARD) {
                BuyerDemandBoardScreen(onAddRequest = { navController.navigate(CREATE_DEMAND) })
            }
            composable(CREATE_DEMAND) {
                CreateDemandScreen(onBack = { navController.popBackStack() }, onPost = { navController.popBackStack() })
            }
            composable(CALENDAR) {
                HarvestCalendarScreen()
            }
            composable(SETTINGS) {
                SettingsScreen(onLogout = {
                    navController.navigate(LOGIN) { popUpTo(0) }
                })
            }
        }
    }
}
